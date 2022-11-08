using System;
using System.Collections;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terminal.Gui;

namespace Volumiser {
    class UI {

        Window topLevel;

        ListView list;

        Label preview;
        
        Label volumeLabel;

        Label itemLabel;

        Label downloadLabel;

        public delegate void ItemDelegate(string itemValue);
       
        public event ItemDelegate ItemSelected;

        public event ItemDelegate ItemChanged;

        public string VolumeLabel { 
            get { return volumeLabel.Text.ToString(); }
            set { volumeLabel.Text = value; } 
        }

        public string ItemLabel {
            get { return itemLabel.Text.ToString(); }
            set { itemLabel.Text = value; }
        }

        public string DownloadLabel {
            get { return downloadLabel.Text.ToString(); }
            set { downloadLabel.Text = value; }
        }

        public string PreviewText {
            get { return preview.Text.ToString(); }
            set { preview.Text = value; }
        }

        public UI() {

            Application.Init();
            Colors.Base.Normal = Application.Driver.MakeAttribute(Color.Gray, Color.Black);

            // Creates the top-level window to show
            topLevel = new Window("Volumiser") {
                X = 0,
                Y = 0,

                // By using Dim.Fill(), it will automatically resize without manual intervention
                Width = Dim.Fill(),
                Height = Dim.Fill(),
            };

            var middleContainer = new Window() {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill() - 1 
            };

            middleContainer.Border.BorderStyle = BorderStyle.None;

            volumeLabel = new Label($"") {
                X = 1, 
                Y = Pos.Bottom(middleContainer),
                Width = Dim.Percent(20),
                Height = 1
            };

            itemLabel = new Label($"") {
                X = Pos.Right(volumeLabel),
                Y = Pos.Bottom(middleContainer),
                Width = Dim.Percent(40),
                Height = 1
            };

            downloadLabel = new Label($"") {
                X = Pos.Right(itemLabel),
                Y = Pos.Bottom(middleContainer),
                Width = Dim.Percent(40),
                Height = 1
            };


            list = new ListView() {
                X = 1,
                Y = Pos.Top(topLevel),
                Width = Dim.Fill(),
                Height = Dim.Percent(55)
            };

            list.Border = new Border() {
               // BorderStyle = BorderStyle.Single,
                //Title = "Filesystem Structure"
            };

            list.OpenSelectedItem += List_OpenSelectedItem;
            list.SelectedItemChanged += List_SelectedItemChanged;

            preview = new Label() {
                X = 1,
                Y = Pos.Bottom(list) + 1,
                Width = Dim.Fill() - 1,
                Height = Dim.Fill() - 1
            };

            preview.Border = new Border() {
                BorderStyle = BorderStyle.Rounded,
                Title = "Preview",                
            };

            middleContainer.Add(list, preview);
            topLevel.Add(volumeLabel, itemLabel, downloadLabel, middleContainer);

            Application.Top.Add(topLevel);

            Application.Top.KeyPress += Top_KeyPress;


        }

        private void Top_KeyPress(View.KeyEventEventArgs obj) {
            if(obj.KeyEvent.Key == Key.Esc)
                Application.RequestStop();
        }

        private void List_SelectedItemChanged(ListViewItemEventArgs obj) {
            ItemChanged?.Invoke((string)obj.Value);
        }

        public void Run() {
            Application.Run();
        }

        public void UpdateCurrentPathItems(IList items) {
            list.SetSource(items);
        }

        private void List_OpenSelectedItem(ListViewItemEventArgs obj) {
            ItemSelected?.Invoke((string)obj.Value);
        }

        public void Refresh() {
            Application.Refresh();
        }

        public bool ShowDownloadDialog(string fileName, string size) {

            var n = MessageBox.Query(" Download File", $"Do you want to download {fileName} ({size}) ", "Yes", "No");
            if (n == 0)
                return true;
            else
                return false;   
        }       
    }
}
