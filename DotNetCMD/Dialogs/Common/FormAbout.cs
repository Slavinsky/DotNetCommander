using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace DotNetCommander
{
    public sealed class FormAbout : Form
    {
        public FormAbout()
        {
            Version version = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0, 0);
            string configPath = SettingsStorage.GetUserConfigPath();

            Text = Language.getString("about");
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(620, 400);
            MinimumSize = new Size(620, 400);
            Font = DialogStyleService.CreateBodyFont();
            Icon = Properties.Resources.icon;

            var iconBitmap = Properties.Resources.icon.ToBitmap();

            var iconBox = new PictureBox
            {
                Dock = DockStyle.Top,
                Height = 72,
                Width = 72,
                SizeMode = PictureBoxSizeMode.CenterImage,
                Image = new Bitmap(iconBitmap, new Size(48, 48)),
                Margin = new Padding(0, 4, 20, 0)
            };

            var titleLabel = new Label
            {
                AutoSize = true,
                Font = DialogStyleService.CreateHeaderFont(),
                Text = "DotNetCommander",
                Margin = new Padding(0, 0, 0, 6)
            };

            var subtitleLabel = new Label
            {
                AutoSize = true,
                Font = DialogStyleService.CreateBodyFont(),
                ForeColor = SystemColors.GrayText,
                Text = Language.getString("aboutSubtitle"),
                Margin = new Padding(0, 0, 0, 10)
            };

            var versionLabel = new Label
            {
                AutoSize = true,
                Font = DialogStyleService.CreateEmphasisFont(),
                ForeColor = Color.FromArgb(40, 84, 125),
                Text = $"{Language.getString("aboutVersion")}: {version}",
                Margin = new Padding(0)
            };

            var headerTextLayout = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0)
            };
            headerTextLayout.Controls.Add(titleLabel);
            headerTextLayout.Controls.Add(subtitleLabel);
            headerTextLayout.Controls.Add(versionLabel);

            var headerLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                Padding = new Padding(20, 20, 20, 12)
            };
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76f));
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            headerLayout.Controls.Add(iconBox, 0, 0);
            headerLayout.Controls.Add(headerTextLayout, 1, 0);

            var descriptionLabel = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(540, 0),
                Font = DialogStyleService.CreateBodyFont(),
                Text = Language.getString("aboutSummary"),
                Margin = new Padding(0, 0, 0, 14)
            };

            var detailsLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                Padding = new Padding(20, 8, 20, 0)
            };
            detailsLayout.RowStyles.Clear();

            AddDetailRow(detailsLayout, Language.getString("aboutPlatform"), Language.getString("aboutPlatformValue"));
            AddDetailRow(detailsLayout, Language.getString("aboutDescription"), Language.getString("aboutCredits"));
            AddDetailRow(detailsLayout, Language.getString("aboutApplication"), "DotNetCommander");

            var storageCaptionLabel = new Label
            {
                AutoSize = true,
                Font = DialogStyleService.CreateCaptionFont(),
                ForeColor = SystemColors.GrayText,
                Text = Language.getString("settingsStorage"),
                Margin = new Padding(0, 12, 0, 4)
            };

            var storagePathTextBox = new TextBox
            {
                Dock = DockStyle.Top,
                ReadOnly = true,
                Font = DialogStyleService.CreateBodyFont(),
                Text = configPath,
                Margin = new Padding(0, 0, 0, 8)
            };

            var storageHintLabel = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(540, 0),
                Font = DialogStyleService.CreateCaptionFont(),
                ForeColor = SystemColors.GrayText,
                Text = Language.getString("settingsStorageFormat"),
                Margin = new Padding(0, 0, 0, 0)
            };

            var contentFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(0, 0, 0, 0)
            };
            contentFlow.Controls.Add(descriptionLabel);
            contentFlow.Controls.Add(detailsLayout);
            contentFlow.Controls.Add(storageCaptionLabel);
            contentFlow.Controls.Add(storagePathTextBox);
            contentFlow.Controls.Add(storageHintLabel);

            var contentHost = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20, 0, 20, 0)
            };
            contentHost.Controls.Add(contentFlow);

            var closeButton = new Button
            {
                DialogResult = DialogResult.OK,
                Text = Language.getString("ok"),
                Width = 110,
                Height = 36
            };

            var buttonPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 64,
                Padding = new Padding(20, 10, 20, 16)
            };
            buttonPanel.Resize += (_, __) => closeButton.Location = new Point(buttonPanel.ClientSize.Width - closeButton.Width, 8);
            buttonPanel.Controls.Add(closeButton);

            Controls.Add(contentHost);
            Controls.Add(buttonPanel);
            Controls.Add(headerLayout);

            AcceptButton = closeButton;
        }

        private static void AddDetailRow(TableLayoutPanel layout, string caption, string value)
        {
            var captionLabel = new Label
            {
                AutoSize = true,
                Font = DialogStyleService.CreateCaptionFont(),
                ForeColor = SystemColors.GrayText,
                Text = caption,
                Margin = new Padding(0, 0, 0, 2)
            };

            var valueLabel = new Label
            {
                AutoSize = true,
                Font = DialogStyleService.CreateBodyFont(),
                Text = value,
                Margin = new Padding(0, 0, 0, 10)
            };

            layout.Controls.Add(captionLabel);
            layout.Controls.Add(valueLabel);
        }
    }
}
