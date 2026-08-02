using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace DotNetCommander
{
    public partial class AddressBar : UserControl
    {
        private String path = null;
        private TextBox textBoxPath = new TextBox();
        private readonly FlowLayoutPanel navigationPanel = new FlowLayoutPanel();
        private readonly ToolTip navigationToolTip = new ToolTip();

        public event EventHandler ButtonClick;
        public event EventHandler BackClick;
        public event EventHandler ParentClick;
        public event EventHandler RefreshClick;

        public delegate void PathChangeHandler(Object sender, String newPath);
        public event PathChangeHandler PathChange;

        public AddressBar()
        {
            InitializeComponent();
            AutoScaleMode = AutoScaleMode.None;
            ConfigureNavigationControls();

            textBoxPath.Multiline = true;
            textBoxPath.Size = new System.Drawing.Size(0, 0);
            textBoxPath.Location = new System.Drawing.Point(0, 0);
            this.Controls.AddRange(new System.Windows.Forms.Control[] { textBoxPath });
            textBoxPath.KeyPress += new System.Windows.Forms.KeyPressEventHandler(textBoxPath_Over);
            textBoxPath.LostFocus += new System.EventHandler(textBoxPath_FocusOver);
            //editBox.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((System.Byte)(0)));
            textBoxPath.Font = this.Font;
            //editBox.BackColor = Color.LightYellow;
            //editBox.BorderStyle = BorderStyle.Fixed3D;
            textBoxPath.Dock = DockStyle.None;
            textBoxPath.SetBounds(104, 0, Math.Max(0, Width - 104), Height);
            textBoxPath.Hide();
            textBoxPath.Text = "";
        }

        private void ConfigureNavigationControls()
        {
            flowLayoutAddressBar.BackColor = SystemColors.Window;
            flowLayoutAddressBar.BorderStyle = BorderStyle.FixedSingle;
            flowLayoutAddressBar.Dock = DockStyle.None;
            flowLayoutAddressBar.Location = new Point(104, 0);
            flowLayoutAddressBar.Padding = new Padding(3, 1, 3, 1);
            flowLayoutAddressBar.Size = new Size(Math.Max(0, Width - 104), Height);

            navigationPanel.AutoSize = false;
            navigationPanel.BackColor = SystemColors.Control;
            navigationPanel.Dock = DockStyle.None;
            navigationPanel.FlowDirection = FlowDirection.LeftToRight;
            navigationPanel.Margin = Padding.Empty;
            navigationPanel.Padding = new Padding(1, 1, 0, 1);
            navigationPanel.Size = new Size(104, Height);
            navigationPanel.WrapContents = false;

            Button backButton = CreateNavigationButton("←", "helpHistoryBack");
            Button parentButton = CreateNavigationButton("↑", "helpNavigateParent");
            Button refreshButton = CreateNavigationButton("↻", "helpRefreshActivePanel");
            backButton.Click += (sender, e) => BackClick?.Invoke(this, e);
            parentButton.Click += (sender, e) => ParentClick?.Invoke(this, e);
            refreshButton.Click += (sender, e) => RefreshClick?.Invoke(this, e);

            navigationPanel.Controls.Add(backButton);
            navigationPanel.Controls.Add(parentButton);
            navigationPanel.Controls.Add(CreateNavigationSeparator());
            navigationPanel.Controls.Add(refreshButton);
            Controls.Add(navigationPanel);
            navigationPanel.BringToFront();
        }

        private Button CreateNavigationButton(string glyph, string tooltipResource)
        {
            var button = new Button
            {
                AutoSize = false,
                BackColor = SystemColors.Control,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Symbol", 12F, FontStyle.Regular, GraphicsUnit.Point),
                Margin = Padding.Empty,
                Size = new Size(31, 28),
                TabStop = false,
                Text = glyph,
                UseVisualStyleBackColor = false
            };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseDownBackColor = SystemColors.ControlDark;
            button.FlatAppearance.MouseOverBackColor = SystemColors.ControlLight;
            navigationToolTip.SetToolTip(button, Language.getString(tooltipResource));
            return button;
        }

        private static Control CreateNavigationSeparator()
        {
            return new Panel
            {
                BackColor = SystemColors.ControlDark,
                Margin = new Padding(1, 5, 1, 5),
                Size = new Size(1, 18)
            };
        }

        // Declare a Name property of type string:
        public String Path
        {
            get
            {
                return path;
            }
            set
            {
                path = value;
                flowLayoutAddressBar.Controls.Clear();
                if (path == null)
                    return;

                String[] splitedPaths = path.Split(System.IO.Path.DirectorySeparatorChar);
                String tempPath = "";
                string[] visibleParts = splitedPaths.Where(part => part.Length > 0).ToArray();
                for (int partIndex = 0; partIndex < visibleParts.Length; partIndex++)
                {
                    string splitPath = visibleParts[partIndex];
                    AddressBarButton pathItem = new AddressBarButton
                    {
                        Text = splitPath,
                        AutoSize = false,
                        BackColor = SystemColors.Window,
                        Font = Font,
                        Margin = Padding.Empty,
                        Padding = Padding.Empty
                    };
                    tempPath += splitPath + System.IO.Path.DirectorySeparatorChar;
                    pathItem.Tag = tempPath;
                    pathItem.Click += new EventHandler(buttonPath_Click);
                    ResizePathButton(pathItem);
                    flowLayoutAddressBar.Controls.Add(pathItem);
                    if (partIndex < visibleParts.Length - 1)
                    {
                        flowLayoutAddressBar.Controls.Add(CreatePathSeparator(pathItem));
                    }
                }
                flowLayoutAddressBar.PerformLayout();
                UpdatePathButtonVisibility();
            }
        }

        private void buttonPath_Click(object sender, EventArgs e)
        {
            if(ButtonClick != null)
                ButtonClick(sender,e);
        }

        private void textBoxPath_Over(object sender, System.Windows.Forms.KeyPressEventArgs e)
        {
            if (e.KeyChar == 13)
            {
                if(PathChange != null)
                    PathChange(this,textBoxPath.Text);
                textBoxPath.Hide();
            }

            if (e.KeyChar == 27)
                textBoxPath.Hide();
        }

        private void textBoxPath_FocusOver(object sender, System.EventArgs e)
        {
            textBoxPath.Hide();
        }

        private void flowLayoutAddressBar_MouseClick(object sender, MouseEventArgs e)
        {
            textBoxPath.Text = path;
            textBoxPath.Show();
            textBoxPath.SelectAll();
            textBoxPath.BringToFront();
            textBoxPath.Focus();
        }

        public void ApplyDisplayFont(Font font)
        {
            Font = font;
            textBoxPath.Font = font;

            foreach (AddressBarButton button in flowLayoutAddressBar.Controls.OfType<AddressBarButton>())
            {
                button.Font = font;
                ResizePathButton(button);
            }
            foreach (Label separator in flowLayoutAddressBar.Controls
                .OfType<Label>()
                .Where(item => item is not AddressBarButton))
            {
                separator.Font = font;
                ResizePathSeparator(separator);
            }
            flowLayoutAddressBar.PerformLayout();
            UpdatePathButtonVisibility();
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            navigationPanel.Location = Point.Empty;
            navigationPanel.Height = Height;
            flowLayoutAddressBar.SetBounds(104, 0, Math.Max(0, Width - 104), Height);
            textBoxPath.SetBounds(104, 0, Math.Max(0, Width - 104), Height);
            foreach (AddressBarButton button in flowLayoutAddressBar.Controls.OfType<AddressBarButton>())
            {
                ResizePathButton(button);
            }
            foreach (Label separator in flowLayoutAddressBar.Controls
                .OfType<Label>()
                .Where(item => item is not AddressBarButton))
            {
                ResizePathSeparator(separator);
            }
            UpdatePathButtonVisibility();
        }

        private void ResizePathButton(AddressBarButton button)
        {
            if (button == null)
                return;

            Size textSize = TextRenderer.MeasureText(
                button.Text,
                button.Font,
                Size.Empty,
                TextFormatFlags.SingleLine);
            button.Size = new Size(
                Math.Max(18, textSize.Width + 6),
                Math.Max(24, flowLayoutAddressBar.ClientSize.Height - 4));
        }

        private Label CreatePathSeparator(AddressBarButton precedingButton)
        {
            var separator = new Label
            {
                AutoSize = false,
                BackColor = SystemColors.Window,
                Font = Font,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                TabStop = false,
                Tag = precedingButton,
                Text = "›",
                TextAlign = ContentAlignment.MiddleCenter
            };
            ResizePathSeparator(separator);
            return separator;
        }

        private void ResizePathSeparator(Label separator)
        {
            Size textSize = TextRenderer.MeasureText(
                separator.Text,
                separator.Font,
                Size.Empty,
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            separator.Size = new Size(
                Math.Max(8, textSize.Width + 2),
                Math.Max(24, flowLayoutAddressBar.ClientSize.Height - 4));
        }

        private void UpdatePathButtonVisibility()
        {
            AddressBarButton[] buttons = flowLayoutAddressBar.Controls
                .OfType<AddressBarButton>()
                .ToArray();
            if (buttons.Length == 0)
                return;

            int requiredWidth = 0;
            for (int index = buttons.Length - 1; index >= 0; index--)
            {
                Label separator = flowLayoutAddressBar.Controls
                    .OfType<Label>()
                    .FirstOrDefault(item => item is not AddressBarButton &&
                        ReferenceEquals(item.Tag, buttons[index]));
                requiredWidth += buttons[index].Width + (separator?.Width ?? 0);
                bool visible = index == buttons.Length - 1 ||
                    requiredWidth < flowLayoutAddressBar.ClientSize.Width - 8;
                buttons[index].Visible = visible;
                if (separator != null)
                    separator.Visible = visible;
            }
        }
    }
}
