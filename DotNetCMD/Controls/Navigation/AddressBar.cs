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

        public event EventHandler ButtonClick;

        public delegate void PathChangeHandler(Object sender, String newPath);
        public event PathChangeHandler PathChange;

        public AddressBar()
        {
            InitializeComponent();

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
            textBoxPath.Dock = DockStyle.Fill;
            textBoxPath.Hide();
            textBoxPath.Text = "";
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

                if (path != null)
                {
                    bool foundButton = false;

                   // flowLayoutAddressBar.SuspendLayout();
                    foreach (AddressBarButton button in flowLayoutAddressBar.Controls)
                    {
                        if ((button.Tag as String) == path)
                        {
                            //button.Select();
                            if (button.Visible)
                            {
                                button.BackColor = SystemColors.ControlLight;
                                foundButton = true;
                            }
                        }
                        else
                        {
                            button.BackColor = SystemColors.InactiveBorder;
                        }
                    }

                    if (foundButton == false)
                    {
                        String[] splitedPaths = path.Split(System.IO.Path.DirectorySeparatorChar);
                        String tempPath = "";

                        flowLayoutAddressBar.Controls.Clear();

                        foreach (String splitPath in splitedPaths)
                        {
                            if (splitPath.Length > 0)
                            {
                                AddressBarButton pathItem = new AddressBarButton();
                                //dirbox.Items.Add(splitPath);
                                pathItem.Text = splitPath;
                                tempPath += splitPath + System.IO.Path.DirectorySeparatorChar;
                                pathItem.Tag = tempPath;
                                //pathItem.SelectedIndex = 0;
                                //pathItem.Width = (pathItem.SelectedItem as String).Length * 7 + 15;
                                //pathItem.Width = pathItem.Text.Length * 7 + 15;
                                pathItem.Width = 0;
                                pathItem.AutoSize = true;
                                pathItem.FlatStyle = FlatStyle.Flat;
                                //pathItem.BackColor = SystemColors.InactiveBorder;
                                pathItem.Margin = new System.Windows.Forms.Padding(0);
                                pathItem.FlatAppearance.BorderColor = SystemColors.ControlDark;
                                pathItem.FlatAppearance.BorderSize = 1;
                                pathItem.Click += new EventHandler(buttonPath_Click);

                                //pathItem.
                                pathItem.PerformLayout();
                                //pathItem.DropDownStyle = ComboBoxStyle.DropDownList;
                                flowLayoutAddressBar.Controls.Add(pathItem);

                            }
                        }
                        flowLayoutAddressBar.PerformLayout();
                        int needWidth = 0;
                        AddressBarButton button;

                        for (int i = flowLayoutAddressBar.Controls.Count; i > 0; i--)
                        {
                            button = flowLayoutAddressBar.Controls[i-1] as AddressBarButton;
                            needWidth += button.Width;
                            button.Visible = (needWidth < (flowLayoutAddressBar.Width-10));

                        }
                        // Show the last Button
                        button = flowLayoutAddressBar.Controls[flowLayoutAddressBar.Controls.Count - 1] as AddressBarButton;
                        button.Visible = true;
                    }
                    //flowLayoutAddressBar.ResumeLayout();
                }
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
            }
        }
    }
}
