using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using EXIF.Exif;
using System.IO;
using System.Security.Cryptography;
using System.CodeDom;
using System.Linq;
using DotNetCommander;

namespace View
{
    public partial class ImageView : Form
    {
        // Хранит информацию о мониторах для текущего сеанса
        private static List<ScreenInfo> _screens = null;
        
        // Статические переменные для хранения размеров и позиции формы между сеансами
        private static Size _savedSize = new Size(0, 0);
        private static Point _savedLocation = new Point(-1, -1);
        
        public ImageView()
        {
            InitializeComponent();
            ShowExifLayout(false);
            
            // Инициализация экранов при первом создании формы
            if (_screens == null)
            {
                InitializeScreens();
            }
        }

        private void InitializeScreens()
        {
            _screens = new List<ScreenInfo>();
            foreach (Screen screen in Screen.AllScreens)
            {
                _screens.Add(new ScreenInfo
                {
                    Bounds = screen.Bounds,
                    Primary = screen.Primary
                });
            }
        }

        private ExifTagCollection _exif;
        private bool _showingExifLayout;

        public void OpenFile(string ImageFile) {
            try
            {
                pictureBox.Image = Image.FromFile(ImageFile);
            }
            catch (Exception)
            {
                listExif.Items.Clear();
                ShowExifLayout(false);
                return;
            }

            LoadExifData(ImageFile);

            // Установка размера формы, если изображение больше экрана
            SetFormSizeToImageIfNecessary();
        }

        private void OpenImageFile()
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = FileTypeClassifier.BuildImageDialogFilter();

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                OpenFile(ofd.FileName);
            }
        }

        private void AddTagToList(ExifTag tag)
        {
            ListViewItem item = listExif.Items.Add(tag.Id.ToString());
            item.SubItems.Add(tag.FieldName);
            item.SubItems.Add(tag.Description);
            item.SubItems.Add(tag.Value);
        }

        private void LookupExifById()
        {
            if (_exif == null)
            {
                return;
            }

            string input = InputBox.Show("EXIF tag ID (empty = show all):", Text, string.Empty);
            if (string.IsNullOrWhiteSpace(input))
            {
                RestoreExifList();
                return;
            }

            try
            {
                int id = Convert.ToInt32(input);

                listExif.Items.Clear();

                ExifTag tag = _exif[id];

                AddTagToList(tag);
            }
            catch (KeyNotFoundException) { }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void LoadExifData(string imageFile)
        {
            _exif = new ExifTagCollection(imageFile);
            RestoreExifList();
            ShowExifLayout(listExif.Items.Count > 0);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                this.Close();
                return true;
            }

            if (keyData == Keys.F1)
            {
                ShowKeyboardHelp();
                return true;
            }

            if (keyData == (Keys.Control | Keys.O))
            {
                OpenImageFile();
                return true;
            }

            if (keyData == (Keys.Control | Keys.L) && _exif != null && listExif.Items.Count > 0)
            {
                LookupExifById();
                return true;
            }

            if (keyData == (Keys.Control | Keys.Shift | Keys.L) && _exif != null)
            {
                RestoreExifList();
                return true;
            }

            if (keyData == (Keys.Control | Keys.Shift | Keys.E))
            {
                ShowExifLayout(true);
                if (tabControl1.TabPages.Contains(tabPage2))
                {
                    tabControl1.SelectedTab = tabPage2;
                }
                return true;
            }
            
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void ShowKeyboardHelp()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Image Viewer");
            builder.AppendLine();
            builder.AppendLine("F1 - Show this help");
            builder.AppendLine("Ctrl+O - Open image file");
            builder.AppendLine("Esc - Close viewer");

            if (_exif != null && listExif.Items.Count > 0)
            {
                builder.AppendLine("Ctrl+L - Filter EXIF by tag ID");
                builder.AppendLine("Ctrl+Shift+L - Show all EXIF tags");
            }

            builder.AppendLine("Ctrl+Shift+E - Force EXIF tabs for debugging");

            MessageBox.Show(this, builder.ToString(), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        
        private void SetFormSizeToImageIfNecessary()
        {
            // Если изображение больше чем текущий экран - уменьшаем форму
            if (pictureBox.Image != null)
            {
                int maxWidth = 0;
                int maxHeight = 0;
                
                // Находим максимальные значения размеров всех экранов
                foreach (var screen in _screens)
                {
                    maxWidth = Math.Max(maxWidth, screen.Bounds.Width);
                    maxHeight = Math.Max(maxHeight, screen.Bounds.Height);
                }
                
                // Если размер изображения больше максимальных размеров экрана
                if (pictureBox.Image.Width > maxWidth || pictureBox.Image.Height > maxHeight)
                {
                    // Рассчитываем подходящий размер
                    Size formSize = new Size(pictureBox.Image.Width, pictureBox.Image.Height);
                    
                    // Если размер формы больше чем максимальный размер экрана, уменьшаем его
                    if (formSize.Width > maxWidth || formSize.Height > maxHeight)
                    {
                        // Рассчитываем масштабирование для вписывания в экран
                        double scaleWidth = (double)maxWidth / pictureBox.Image.Width;
                        double scaleHeight = (double)maxHeight / pictureBox.Image.Height;
                        
                        // Используем меньший масштаб
                        double scale = Math.Min(scaleWidth, scaleHeight);
                        
                        formSize.Width = (int)(pictureBox.Image.Width * scale);
                        formSize.Height = (int)(pictureBox.Image.Height * scale);
                    }
                    
                    // Добавляем немного места для рамок формы
                    formSize.Width += 30;
                    formSize.Height += 70;
                    
                    this.Size = formSize;
                }
            }
        }
        
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Сохраняем размер и позицию формы перед закрытием
            _savedSize = this.Size;
            _savedLocation = this.Location;
            
            base.OnFormClosing(e);
        }
        
        protected override void OnLoad(EventArgs e)
        {
            // Устанавливаем сохраненный размер и позицию формы, если они существуют
            if (_savedSize.Width > 0 && _savedSize.Height > 0)
            {
                this.Size = _savedSize;
            }
            
            if (_savedLocation.X >= 0 && _savedLocation.Y >= 0)
            {
                // Проверяем, находится ли позиция внутри экрана
                Point location = _savedLocation;
                
                bool locationIsValid = false;
                foreach (var screen in _screens)
                {
                    if (screen.Bounds.Contains(location))
                    {
                        locationIsValid = true;
                        break;
                    }
                }
                
                // Если позиция не валидна, устанавливаем значения по умолчанию
                if (locationIsValid)
                {
                    this.Location = location;
                }
                else
                {
                    // Устанавливаем форму в центр главного экрана
                    Screen primaryScreen = Screen.PrimaryScreen;
                    this.Location = new Point(
                        (primaryScreen.Bounds.Width - this.Size.Width) / 2,
                        (primaryScreen.Bounds.Height - this.Size.Height) / 2);
                }
            }
            
            base.OnLoad(e);
        }

        private void RestoreExifList()
        {
            listExif.Items.Clear();
            if (_exif == null)
            {
                return;
            }

            foreach (ExifTag tag in _exif)
            {
                AddTagToList(tag);
            }
        }

        private void ShowExifLayout(bool hasExif)
        {
            bool layoutAlreadyApplied = hasExif
                ? tabControl1.Visible && groupBox1.Parent == tabPage1
                : !tabControl1.Visible && groupBox1.Parent == this;

            if (_showingExifLayout == hasExif && layoutAlreadyApplied)
            {
                return;
            }

            SuspendLayout();
            tabControl1.SuspendLayout();

            if (hasExif)
            {
                if (groupBox1.Parent != tabPage1)
                {
                    Controls.Remove(groupBox1);
                    tabPage1.Controls.Add(groupBox1);
                }

                groupBox1.Dock = DockStyle.Fill;
                tabControl1.Visible = true;
                tabControl1.SelectedTab = tabPage1;
            }
            else
            {
                if (groupBox1.Parent != this)
                {
                    tabPage1.Controls.Remove(groupBox1);
                    Controls.Add(groupBox1);
                    Controls.SetChildIndex(groupBox1, 0);
                }

                groupBox1.Dock = DockStyle.Fill;
                tabControl1.Visible = false;
                groupBox1.BringToFront();
            }

            _showingExifLayout = hasExif;
            tabControl1.ResumeLayout();
            ResumeLayout();
        }
    }
    
    // Вспомогательный класс для информации о мониторах
    public class ScreenInfo
    {
        public Rectangle Bounds { get; set; }
        public bool Primary { get; set; }
    }
}
