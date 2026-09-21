namespace NutDesk
{
    partial class About_Gui
    {
        // Form remplace la méthode Dispose pour nettoyer la liste des composants.
        [System.Diagnostics.DebuggerNonUserCode()]
        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing && components != null)
                {
                    components.Dispose();
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        // Requise par le Concepteur Windows Form
        private System.ComponentModel.IContainer components = null;

        // REMARQUE : la procédure suivante est requise par le Concepteur Windows Form
        // Elle peut être modifiée à l'aide du Concepteur Windows Form.
        // Ne la modifiez pas à l'aide de l'éditeur de code.
        [System.Diagnostics.DebuggerStepThrough()]
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(About_Gui));
            this.GBox = new System.Windows.Forms.GroupBox();
            this.LkLbl_Github = new System.Windows.Forms.LinkLabel();
            this.Lbl_Github = new System.Windows.Forms.Label();
            this.Lbl_Sf = new System.Windows.Forms.Label();
            this.Lbl_Copyright_2019 = new System.Windows.Forms.Label();
            this.Lbl_Copyright_2006 = new System.Windows.Forms.Label();
            this.Lbl_ProgNameVersion = new System.Windows.Forms.Label();
            this.PictureBox1 = new System.Windows.Forms.PictureBox();
            this.Btn_OK = new System.Windows.Forms.Button();
            this.Label_License = new System.Windows.Forms.Label();
            this.GBox.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)this.PictureBox1).BeginInit();
            this.SuspendLayout();
            //
            // GBox
            //
            this.GBox.Controls.Add(this.Label_License);
            this.GBox.Controls.Add(this.LkLbl_Github);
            this.GBox.Controls.Add(this.Lbl_Github);
            this.GBox.Controls.Add(this.Lbl_Sf);
            this.GBox.Controls.Add(this.Lbl_Copyright_2019);
            this.GBox.Controls.Add(this.Lbl_Copyright_2006);
            this.GBox.Controls.Add(this.Lbl_ProgNameVersion);
            this.GBox.Controls.Add(this.PictureBox1);
            this.GBox.Location = new System.Drawing.Point(4, 4);
            this.GBox.Name = "GBox";
            this.GBox.Size = new System.Drawing.Size(360, 364);
            this.GBox.TabIndex = 0;
            this.GBox.TabStop = false;
            //
            // LkLbl_Github
            //
            this.LkLbl_Github.AutoSize = true;
            this.LkLbl_Github.Location = new System.Drawing.Point(8, 167);
            this.LkLbl_Github.Name = "LkLbl_Github";
            this.LkLbl_Github.Size = new System.Drawing.Size(215, 13);
            this.LkLbl_Github.TabIndex = 6;
            this.LkLbl_Github.TabStop = true;
            this.LkLbl_Github.Text = "https://github.com/kriseniprak/NutDesk";
            //
            // Lbl_Github
            //
            this.Lbl_Github.AutoSize = true;
            this.Lbl_Github.Location = new System.Drawing.Point(8, 154);
            this.Lbl_Github.Name = "Lbl_Github";
            this.Lbl_Github.Size = new System.Drawing.Size(146, 13);
            this.Lbl_Github.TabIndex = 5;
            this.Lbl_Github.Text = "Source Available from GitHub";
            //
            // Lbl_Sf
            //
            this.Lbl_Sf.AutoSize = true;
            this.Lbl_Sf.Location = new System.Drawing.Point(8, 119);
            this.Lbl_Sf.Name = "Lbl_Sf";
            this.Lbl_Sf.Size = new System.Drawing.Size(220, 26);
            this.Lbl_Sf.TabIndex = 4;
            this.Lbl_Sf.Text = "Based from Winnut Sf\r\nhttps://sourceforge.net/projects/winnutclient";
            //
            // Lbl_Copyright_2019
            //
            this.Lbl_Copyright_2019.AutoSize = true;
            this.Lbl_Copyright_2019.Location = new System.Drawing.Point(122, 90);
            this.Lbl_Copyright_2019.Name = "Lbl_Copyright_2019";
            this.Lbl_Copyright_2019.Size = new System.Drawing.Size(179, 26);
            this.Lbl_Copyright_2019.TabIndex = 3;
            this.Lbl_Copyright_2019.Text = "Copyright Gawindx (Decaux Nicolas)\r\n2019-2020";
            //
            // Lbl_Copyright_2006
            //
            this.Lbl_Copyright_2006.AutoSize = true;
            this.Lbl_Copyright_2006.Location = new System.Drawing.Point(122, 53);
            this.Lbl_Copyright_2006.Name = "Lbl_Copyright_2006";
            this.Lbl_Copyright_2006.Size = new System.Drawing.Size(137, 26);
            this.Lbl_Copyright_2006.TabIndex = 2;
            this.Lbl_Copyright_2006.Text = "Copyright Michael Liberman\r\n©  2006-2007";
            //
            // Lbl_ProgNameVersion
            //
            this.Lbl_ProgNameVersion.AutoSize = true;
            this.Lbl_ProgNameVersion.Location = new System.Drawing.Point(119, 20);
            this.Lbl_ProgNameVersion.Name = "Lbl_ProgNameVersion";
            this.Lbl_ProgNameVersion.Size = new System.Drawing.Size(184, 13);
            this.Lbl_ProgNameVersion.TabIndex = 1;
            this.Lbl_ProgNameVersion.Text = "AppGlobals.LongProgramName";
            //
            // PictureBox1
            //
            this.PictureBox1.Image = (System.Drawing.Image)global::NutDesk.Properties.Resources.ups_104x104;
            this.PictureBox1.Location = new System.Drawing.Point(8, 12);
            this.PictureBox1.Name = "PictureBox1";
            this.PictureBox1.Size = new System.Drawing.Size(104, 104);
            this.PictureBox1.TabIndex = 0;
            this.PictureBox1.TabStop = false;
            //
            // Btn_OK
            //
            this.Btn_OK.Location = new System.Drawing.Point(157, 372);
            this.Btn_OK.Name = "Btn_OK";
            this.Btn_OK.Size = new System.Drawing.Size(70, 23);
            this.Btn_OK.TabIndex = 1;
            this.Btn_OK.Text = "OK";
            this.Btn_OK.UseVisualStyleBackColor = true;
            //
            // Label_License
            //
            this.Label_License.AutoSize = true;
            this.Label_License.Location = new System.Drawing.Point(8, 200);
            this.Label_License.Name = "Label_License";
            this.Label_License.Size = new System.Drawing.Size(344, 156);
            this.Label_License.TabIndex = 7;
            this.Label_License.Text = resources.GetString("Label_License.Text");
            //
            // About_Gui
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.ClientSize = new System.Drawing.Size(368, 401);
            this.Controls.Add(this.Btn_OK);
            this.Controls.Add(this.GBox);
            this.Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "About_Gui";
            this.Text = "About";
            this.GBox.ResumeLayout(false);
            this.GBox.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)this.PictureBox1).EndInit();
            this.ResumeLayout(false);
        }

        internal System.Windows.Forms.GroupBox GBox;
        internal System.Windows.Forms.PictureBox PictureBox1;
        internal System.Windows.Forms.Label Lbl_ProgNameVersion;
        internal System.Windows.Forms.Label Lbl_Copyright_2019;
        internal System.Windows.Forms.Label Lbl_Copyright_2006;
        internal System.Windows.Forms.Label Lbl_Github;
        internal System.Windows.Forms.Label Lbl_Sf;
        internal System.Windows.Forms.LinkLabel LkLbl_Github;
        internal System.Windows.Forms.Button Btn_OK;
        internal System.Windows.Forms.Label Label_License;
    }
}
