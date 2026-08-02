using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace DotNetCommander
{
    public partial class AddressBarButton : Label
    {
        private bool isActive;

        public AddressBarButton()
        {
            InitializeComponent();
            AutoEllipsis = true;
            TextAlign = ContentAlignment.MiddleCenter;
            UseCompatibleTextRendering = false;
        }

        public bool IsActive
        {
            get => isActive;
            set
            {
                isActive = value;
                BackColor = value ? SystemColors.ControlLight : SystemColors.Window;
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            if (!isActive)
                BackColor = SystemColors.ControlLight;
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (!isActive)
                BackColor = SystemColors.Window;
        }
    }
}
