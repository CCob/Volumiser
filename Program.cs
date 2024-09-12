using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using DiscUtils;
using DiscUtils.Ebs;
using DiscUtils.Setup;
using DiscUtils.Streams;
using DiscUtils.Ntfs;
using Mono.Options;
using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text;

namespace DiscImage.EbsDiscTest {
    class Program {

        static string CurrentPath = "";

        static DiscFileSystem CurrentFileSystem;

        static VolumeManager VolumeManager;

        static Volumiser.UI UI;

        static IEnumerable<string> Volumes;

        static Task DownloadTask;

        static string PathSeparator;


        static VirtualDisk GetEBSDiskImage(string snapshotId, string profile, string awsAccessKey, string awsSecret, string region) {

            AWSCredentials credentials = null;

            if(profile != null) {
                var chain = new CredentialProfileStoreChain();
                if (!chain.TryGetAWSCredentials(profile, out credentials)) {
                    throw new ArgumentException($"[!] Failed to find profile with name {profile}");
                }              
            }else if(awsAccessKey != null) {
                credentials = new BasicAWSCredentials(awsAccessKey, awsSecret);
            }

            var result = new Disk(snapshotId, region, credentials);
            return result;
        }

        static VirtualDisk GetLocalDiskImage(string path) {
            return VirtualDisk.OpenDisk(path, FileAccess.Read);
        }


        static void Main(string[] args) {

            bool showHelp = false;
            string profile = null;
            string awsAccessKey = null;
            string awsAccessSecret = null;
            string awsRegion = null;
            string command = null;
            string image = null;
            string path = "";

            OptionSet option_set = new OptionSet()
                .Add("h|help", "Display this help", v => showHelp = v != null)
                .Add("awsprofile=", "AWS profile to use", v => profile = v)
                .Add("awskey=", "AWS access key to use", v => awsAccessKey = v)
                .Add("awssecret=", "AWS secret", v => awsAccessSecret = v)
                .Add("awsregion=", "AWS region", v => awsRegion = v)
                .Add("command=", "The command to execute: [volumes|ls|extract]", v => command = v)
                .Add("image=", @"The path to the virtual disk, e.g. C:\temp\backup.vhdx or ebs://snapshotid", v => image = v)
                .Add("path=", @"Volume and path to query within the virtual disk filesystem ", v => path = v);

            try {

                option_set.Parse(args);

                if(image == null) {
                    Console.WriteLine("[!] Image path not specified");
                    showHelp = true;
                }

                if (showHelp) {
                    option_set.WriteOptionDescriptions(Console.Out);
                    return;
                }

            } catch (Exception e) {
                Console.WriteLine($"[!] Failed to parse arguments: {e.Message}");
                option_set.WriteOptionDescriptions(Console.Out);
                return;
            }

            if (showHelp) {
                option_set.WriteOptionDescriptions(Console.Out);
                return;
            }

            DiscUtils.Containers.SetupHelper.SetupContainers();
            DiscUtils.FileSystems.SetupHelper.SetupFileSystems(); 
            SetupHelper.RegisterAssembly(typeof(DiskImageFile).Assembly);
    
            VirtualDisk disk;

            try {

                if (image.StartsWith("ebs://")) {
                    disk = GetEBSDiskImage(image.Substring(6), profile, awsAccessKey, awsAccessSecret, awsRegion);
                    var ebsDisk = (Disk)disk;
                } else if (image.StartsWith(@"\\.\")) {
                    disk = new DiscUtils.RawDisk.Disk(image);
                } else {
                    disk = GetLocalDiskImage(image);
                }

                if (disk == null) {
                    Console.WriteLine($"[!] Failed to open disk {image}");
                    return;
                }

                VolumeManager = new VolumeManager(disk);

                if (command != null) {

                    Console.WriteLine($"[+] Opened disk image, Size: {disk.Capacity / 1024 / 1024 / 1024}GB");

                    if (command == "volumes") {

                        foreach (var volume in VolumeManager.GetLogicalVolumes()) {

                            var volumeStream = volume.Open();
                            var fsType = FileSystemManager.DetectFileSystems(volumeStream);

                            try {
                                Console.WriteLine($"\tVolume ID: {volume.Identity}, Size: {volume.Length / 1024 / 1024} MB, Type: {(fsType.Length > 0 ? fsType[0].Description : "Unknown")}");
                            } catch (Exception) { }
                        }

                    } else if (command == "ls" || command == "download") {

                        var pathStart = path.LastIndexOf(":");
                        var volumePath = path.Substring(0, pathStart);
                        var filePath = path.Substring(pathStart + 1);
                        var volume = VolumeManager.GetVolume(volumePath);

                        var sparseStream = volume.Open();
                        Console.WriteLine($"[+] Opened volume with ID {volumePath}");

                        var fsInfo = FileSystemManager.DetectFileSystems(sparseStream);
                        var fs = fsInfo[0].Open(sparseStream);
                        if (fs is NtfsFileSystem)
                        {
                            ((NtfsFileSystem)fs).NtfsOptions.HideHiddenFiles = false;
                            ((NtfsFileSystem)fs).NtfsOptions.HideSystemFiles = false;
                        }
                        if (command == "ls") {

                            foreach (var file in fs.GetDirectories(filePath)) {
                                var dirInfo = fs.GetDirectoryInfo(file);
                                Console.WriteLine($"{dirInfo.LastWriteTimeUtc}  {"DIR",-15} {Path.GetFileName(file)}");
                            }

                            foreach (var file in fs.GetFiles(filePath)) {
                                var fileInfo = fs.GetFileInfo(file);
                                Console.WriteLine($"{fileInfo.LastWriteTimeUtc} { $"{fileInfo.Length / 1024.0 / 1024.0: 0.00}MB",-16} {Path.GetFileName(file)}");
                            }

                        } else {

                            var fileStream = fs.OpenFile(filePath, FileMode.Open, FileAccess.Read);
                            Console.WriteLine($"[+] Opened file with path {filePath} for with size: {fileStream.Length}");
                            File.WriteAllBytes(Path.GetFileName(filePath), StreamUtilities.ReadExact(fileStream, (int)fileStream.Length));
                        }
                    }
                } else {

                    Volumes = VolumeManager.GetLogicalVolumes().Select(v => v.Identity);
                    UI = new Volumiser.UI();
                    UI.ItemSelected += Ui_ItemSelected;
                    UI.ItemChanged += UI_ItemChanged;
                    UI.UpdateCurrentPathItems(Volumes.ToList());

                    if (Volumes.Count() > 0)
                        UI_ItemChanged(Volumes.First());

                    UI.Run();
                }

            }catch(Exception e) {
                Console.WriteLine($"[!] {e.Message}");
            }
        }

        private static void UI_ItemChanged(string itemValue) {

            string itemLabel;
           
            if (itemValue != ".." && CurrentPath != null && CurrentFileSystem != null) {

                itemLabel = $"Path: {CurrentPath}\\{itemValue}";

                if ((CurrentFileSystem.GetAttributes($"{CurrentPath}\\{itemValue}") & FileAttributes.Directory) != FileAttributes.Directory) {
                    var fileLen = CurrentFileSystem.GetFileLength($"{CurrentPath}\\{itemValue}");
                    itemLabel += $" {GetHumanSize(fileLen)}";

                    if(fileLen < 10240 && fileLen > 0 && (itemValue.EndsWith(".txt") || itemValue.EndsWith(".log") || itemValue.EndsWith(".config") || itemValue.EndsWith(".ini") || itemValue.EndsWith(".xml"))) {

                        using (var fileStream = CurrentFileSystem.OpenFile($"{CurrentPath}\\{itemValue}", FileMode.Open, FileAccess.Read)) {
                            
                            var fileData = StreamUtilities.ReadExact(fileStream, (int)fileLen);
                            var encoding = Encoding.UTF8;
                                                        
                            if((fileData[0] == 0xff && fileData[1] == 0xfe) || fileData[1] == 0) {
                                encoding = Encoding.Unicode;
                                fileData = fileData.Skip(2).ToArray();
                            } else if(fileData[0] == 0xfe && fileData[1] == 0xff) {
                                encoding = Encoding.BigEndianUnicode;
                                fileData = fileData.Skip(2).ToArray();
                            }

                            UI.PreviewText = encoding.GetString(fileData);
                        }

                    } else {
                        UI.PreviewText = "";
                    }
                }

            } else {

                if (CurrentFileSystem == null) {
                    var volume = VolumeManager.GetVolume(itemValue);

                    using (var sparseStream = volume.Open()) {
                        var fsInfo = FileSystemManager.DetectFileSystems(sparseStream);
                        if (fsInfo != null && fsInfo.Length > 0) {
                            var fileSystem = fsInfo[0].Open(sparseStream);
                            CurrentPath = "";
                            UI.VolumeLabel = $"Volume: {fileSystem.FriendlyName} {GetHumanSize(fileSystem.Size)}";
                        } else {
                            UI.VolumeLabel = $"Volume: Unknown {GetHumanSize(volume.Length)}";
                        }
                    }
                }

                itemLabel = $"Path: {CurrentPath}";
            }

            UI.ItemLabel = itemLabel.Replace("\\",PathSeparator);
        }

        static private async Task DownloadFile(string volumePath) {

            using (var fileStream = CurrentFileSystem.OpenFile(volumePath, FileMode.Open, FileAccess.Read)) {
                using (var outputStream = new FileStream(Path.GetFileName(volumePath), FileMode.OpenOrCreate, FileAccess.ReadWrite)) {
                    byte[] buffer = new byte[512 * 1024];
                    long totalRead = 0;

                    while (fileStream.Position < fileStream.Length) {
                        int readSize = await fileStream.ReadAsync(buffer, 0, buffer.Length);
                        await outputStream.WriteAsync(buffer, 0, readSize);
                        totalRead += readSize;
                        float progress = (totalRead / (float)fileStream.Length) * 100;
                        UI.DownloadLabel = $"Downloading ({progress:0.0}%): {volumePath}";  
                    }
                }
            }
        }

        private static void Ui_ItemSelected(string itemValue) {

            if (CurrentFileSystem == null) {

                var volume = VolumeManager.GetVolume(itemValue);

                using (var sparseStream = volume.Open()) { 
                    var fsInfo = FileSystemManager.DetectFileSystems(sparseStream);
                    if (fsInfo != null && fsInfo.Length > 0) {
                        CurrentFileSystem = fsInfo[0].Open(sparseStream);
                        CurrentPath = "";
                        UI.VolumeLabel = $"Volume: {CurrentFileSystem.FriendlyName} {GetHumanSize(CurrentFileSystem.Size)}";
                        if (CurrentFileSystem is NtfsFileSystem)
                        {
                            ((NtfsFileSystem)CurrentFileSystem).NtfsOptions.HideHiddenFiles = false;
                            ((NtfsFileSystem)CurrentFileSystem).NtfsOptions.HideSystemFiles = false;
                        }
                        UpdateView();
                    }
                }
                            
            } else {

                if (itemValue == ".." && CurrentPath == "") {
                    UI.UpdateCurrentPathItems(Volumes.ToList());
                    CurrentFileSystem = null;
                    CurrentPath = null;
                    UI.VolumeLabel = "";                 
                } else {

                    if (itemValue != "..") {

                        if (CurrentFileSystem.FriendlyName == "Microsoft NTFS" || CurrentFileSystem.FriendlyName.Contains("FAT")) {
                            PathSeparator = "\\";
                        } else {
                            PathSeparator = "/";
                        }                   

                        if ((CurrentFileSystem.GetAttributes($"{CurrentPath}\\{itemValue}") & FileAttributes.Directory) == FileAttributes.Directory) {
                            CurrentPath += $"\\{itemValue}";
                            UpdateView();
                        } else {

                            var selectedFile = $"{CurrentPath}\\{itemValue}";
                            var fileInfo = CurrentFileSystem.GetFileInfo(selectedFile);
                            
                            if(UI.ShowDownloadDialog(itemValue, GetHumanSize(fileInfo.Length))) {

                                if(DownloadTask != null) {
                                    DownloadTask = DownloadTask.ContinueWith(t => DownloadFile(selectedFile)).ContinueWith(t => {
                                        DownloadTask = null;
                                        UI.DownloadLabel = "Downloads Complete";
                                    });
                                } else {
                                    DownloadTask = DownloadFile(selectedFile).ContinueWith(t => {
                                        DownloadTask = null;
                                        UI.DownloadLabel = "Downloads Complete";
                                    });
                                }                                                               
                            }
                        }

                    } else {
                        CurrentPath = CurrentPath.Substring(0, CurrentPath.LastIndexOf('\\'));
                        UpdateView();
                    }                    
                }
            }          
        }

        static string GetHumanSize(long size) {
            if (size < 1024) {
                return $"{size}";
            } else if (size < 1024L * 1024L) {
                return $"{size / 1024.0:0.00}KB";
            } else if (size < 1024L * 1024L * 1024L) {
                return $"{size / 1024.0 / 1024.0:0.00}MB";
            } else if (size < 1024L * 1024L * 1024L * 1024L) {
                return $"{size / 1024.0 / 1024.0 / 1024.0:0.00}GB";
            } else {
                return $"{size / 1024.0 / 1024.0 / 1024.0 / 1024.0:0.00}TB";
            }
        }
        
        private static void UpdateView() {
            UI.UpdateCurrentPathItems(new string[] { ".." }
            .Concat(CurrentFileSystem.GetDirectories(CurrentPath).Select(pi => Path.GetFileName(pi))
            .Concat(CurrentFileSystem.GetFiles(CurrentPath)).Select(pi => Path.GetFileName(pi)))
            .ToList());
        }
    }
}
