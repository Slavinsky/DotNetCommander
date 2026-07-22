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
    public partial class AddressBarButton : Button
    {
        public AddressBarButton()
        {
            InitializeComponent();
        }

        protected override void OnPaint(PaintEventArgs pe)
        {
            base.OnPaint(pe);
            ControlPaint.DrawBorder(pe.Graphics,pe.ClipRectangle,
                                    FlatAppearance.BorderColor, FlatAppearance.BorderSize, ButtonBorderStyle.Solid,
                                    FlatAppearance.BorderColor, 0, ButtonBorderStyle.Solid,
                                    FlatAppearance.BorderColor, FlatAppearance.BorderSize, ButtonBorderStyle.Solid,
                                    FlatAppearance.BorderColor, 0, ButtonBorderStyle.Solid);
        }
    }
}
