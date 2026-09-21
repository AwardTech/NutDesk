// WinNUT-Client is a NUT windows client for monitoring your ups hooked up to your favorite linux server.
// Copyright (C) 2019-2021 Gawindx (Decaux Nicolas)
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or any later version.
//
// This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY

using System;
using System.Drawing;
using System.Windows.Forms;

namespace NutDesk.WinFormControls
{
    public class CProgressBar : ProgressBar
    {
        private const int WmPaint = 15;

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams result = base.CreateParams;
                if (Environment.OSVersion.Platform == PlatformID.Win32NT && Environment.OSVersion.Version.Major >= 6)
                {
                    byte[] vIn = new byte[] { 0, 2, 0, 0, 0, 0, 0, 0 };
                    result.ExStyle = result.ExStyle | BitConverter.ToInt32(vIn, 0); // WS_EX_COMPOSITED
                }
                return result;
            }
        }

        protected override void WndProc(ref System.Windows.Forms.Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == WmPaint)
            {
                // Both of these used to be left to the finalizer, once per repaint.
                using (Graphics graphics = CreateGraphics())
                {
                    using (var brush = new SolidBrush(ForeColor))
                    {
                        SizeF textSize = graphics.MeasureString(Text, Font);
                        graphics.DrawString(Text, Font, brush,
                                            (Width - textSize.Width) / 2, (Height - textSize.Height) / 2);
                    }
                }
            }
        }

        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Always)]
        [System.ComponentModel.Browsable(true)]
        public override string Text
        {
            get => base.Text;
            set
            {
                base.Text = value;
                Refresh();
            }
        }

        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Always)]
        [System.ComponentModel.Browsable(true)]
        public override Font Font
        {
            get => base.Font;
            set
            {
                base.Font = value;
                Refresh();
            }
        }
    }
}
