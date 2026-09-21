namespace NutDesk
{
    partial class Shutdown_Gui
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
        private System.ComponentModel.IContainer components;

        // REMARQUE : la procédure suivante est requise par le Concepteur Windows Form
        // Elle peut être modifiée à l'aide du Concepteur Windows Form.
        // Ne la modifiez pas à l'aide de l'éditeur de code.
        [System.Diagnostics.DebuggerStepThrough()]
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Shutdown_Gui));
            this.Grace_Button = new System.Windows.Forms.Button();
            this.ShutDown_Btn = new System.Windows.Forms.Button();
            this.lbl_UPSStatus = new System.Windows.Forms.Label();
            this.Run_Timer = new System.Windows.Forms.Timer(this.components);
            this.SuspendLayout();
            //
            // Grace_Button
            //
            resources.ApplyResources(this.Grace_Button, "Grace_Button");
            this.Grace_Button.Name = "Grace_Button";
            this.Grace_Button.UseVisualStyleBackColor = true;
            this.Grace_Button.UseWaitCursor = true;
            //
            // ShutDown_Btn
            //
            resources.ApplyResources(this.ShutDown_Btn, "ShutDown_Btn");
            this.ShutDown_Btn.Name = "ShutDown_Btn";
            this.ShutDown_Btn.UseVisualStyleBackColor = true;
            this.ShutDown_Btn.UseWaitCursor = true;
            //
            // lbl_UPSStatus
            //
            resources.ApplyResources(this.lbl_UPSStatus, "lbl_UPSStatus");
            this.lbl_UPSStatus.ForeColor = System.Drawing.SystemColors.ControlText;
            this.lbl_UPSStatus.Name = "lbl_UPSStatus";
            this.lbl_UPSStatus.UseWaitCursor = true;
            //
            // Run_Timer
            //
            this.Run_Timer.Enabled = true;
            this.Run_Timer.Interval = 1000;
            //
            // Shutdown_Gui
            //
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.Controls.Add(this.lbl_UPSStatus);
            this.Controls.Add(this.ShutDown_Btn);
            this.Controls.Add(this.Grace_Button);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "Shutdown_Gui";
            this.TopMost = true;
            this.UseWaitCursor = true;
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        internal System.Windows.Forms.Button Grace_Button;
        internal System.Windows.Forms.Button ShutDown_Btn;
        internal System.Windows.Forms.Label lbl_UPSStatus;
        internal System.Windows.Forms.Timer Run_Timer;
    }
}
