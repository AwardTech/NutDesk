namespace NutDesk
{
    partial class List_Var_Gui
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(List_Var_Gui));
            this.TView_UPSVar = new System.Windows.Forms.TreeView();
            this.GB1 = new System.Windows.Forms.GroupBox();
            this.Lbl_D_Value = new System.Windows.Forms.Label();
            this.Lbl_V_Value = new System.Windows.Forms.Label();
            this.Lbl_N_Value = new System.Windows.Forms.Label();
            this.Lbl_D = new System.Windows.Forms.Label();
            this.Lbl_V = new System.Windows.Forms.Label();
            this.Lbl_Name = new System.Windows.Forms.Label();
            this.Btn_Reload = new System.Windows.Forms.Button();
            this.Btn_Close = new System.Windows.Forms.Button();
            this.Timer_Update_List = new System.Windows.Forms.Timer(this.components);
            this.Btn_Clip = new System.Windows.Forms.Button();
            this.ToolTip = new System.Windows.Forms.ToolTip(this.components);
            this.Btn_Save = new System.Windows.Forms.Button();
            this.GB1.SuspendLayout();
            this.SuspendLayout();
            //
            // TView_UPSVar
            //
            resources.ApplyResources(this.TView_UPSVar, "TView_UPSVar");
            this.TView_UPSVar.Name = "TView_UPSVar";
            this.TView_UPSVar.PathSeparator = ".";
            //
            // GB1
            //
            this.GB1.Controls.Add(this.Lbl_D_Value);
            this.GB1.Controls.Add(this.Lbl_V_Value);
            this.GB1.Controls.Add(this.Lbl_N_Value);
            this.GB1.Controls.Add(this.Lbl_D);
            this.GB1.Controls.Add(this.Lbl_V);
            this.GB1.Controls.Add(this.Lbl_Name);
            resources.ApplyResources(this.GB1, "GB1");
            this.GB1.Name = "GB1";
            this.GB1.TabStop = false;
            //
            // Lbl_D_Value
            //
            resources.ApplyResources(this.Lbl_D_Value, "Lbl_D_Value");
            this.Lbl_D_Value.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.Lbl_D_Value.Name = "Lbl_D_Value";
            //
            // Lbl_V_Value
            //
            resources.ApplyResources(this.Lbl_V_Value, "Lbl_V_Value");
            this.Lbl_V_Value.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.Lbl_V_Value.Name = "Lbl_V_Value";
            //
            // Lbl_N_Value
            //
            resources.ApplyResources(this.Lbl_N_Value, "Lbl_N_Value");
            this.Lbl_N_Value.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.Lbl_N_Value.ForeColor = System.Drawing.SystemColors.ControlText;
            this.Lbl_N_Value.Name = "Lbl_N_Value";
            //
            // Lbl_D
            //
            resources.ApplyResources(this.Lbl_D, "Lbl_D");
            this.Lbl_D.Name = "Lbl_D";
            //
            // Lbl_V
            //
            resources.ApplyResources(this.Lbl_V, "Lbl_V");
            this.Lbl_V.Name = "Lbl_V";
            //
            // Lbl_Name
            //
            resources.ApplyResources(this.Lbl_Name, "Lbl_Name");
            this.Lbl_Name.Name = "Lbl_Name";
            //
            // Btn_Reload
            //
            resources.ApplyResources(this.Btn_Reload, "Btn_Reload");
            this.Btn_Reload.Name = "Btn_Reload";
            this.ToolTip.SetToolTip(this.Btn_Reload, resources.GetString("Btn_Reload.ToolTip"));
            this.Btn_Reload.UseVisualStyleBackColor = true;
            //
            // Btn_Close
            //
            resources.ApplyResources(this.Btn_Close, "Btn_Close");
            this.Btn_Close.Name = "Btn_Close";
            this.ToolTip.SetToolTip(this.Btn_Close, resources.GetString("Btn_Close.ToolTip"));
            this.Btn_Close.UseVisualStyleBackColor = true;
            //
            // Timer_Update_List
            //
            this.Timer_Update_List.Interval = 1000;
            //
            // Btn_Clip
            //
            this.Btn_Clip.Image = (System.Drawing.Image)global::NutDesk.Properties.Resources.CopyHS;
            resources.ApplyResources(this.Btn_Clip, "Btn_Clip");
            this.Btn_Clip.Name = "Btn_Clip";
            this.ToolTip.SetToolTip(this.Btn_Clip, resources.GetString("Btn_Clip.ToolTip"));
            this.Btn_Clip.UseVisualStyleBackColor = true;
            //
            // Btn_Save
            //
            this.Btn_Save.Image = (System.Drawing.Image)global::NutDesk.Properties.Resources.saveHS;
            resources.ApplyResources(this.Btn_Save, "Btn_Save");
            this.Btn_Save.Name = "Btn_Save";
            this.ToolTip.SetToolTip(this.Btn_Save, resources.GetString("Btn_Save.ToolTip"));
            this.Btn_Save.UseVisualStyleBackColor = true;
            //
            // List_Var_Gui
            //
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.Controls.Add(this.Btn_Save);
            this.Controls.Add(this.Btn_Clip);
            this.Controls.Add(this.Btn_Close);
            this.Controls.Add(this.Btn_Reload);
            this.Controls.Add(this.GB1);
            this.Controls.Add(this.TView_UPSVar);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "List_Var_Gui";
            this.GB1.ResumeLayout(false);
            this.GB1.PerformLayout();
            this.ResumeLayout(false);
        }

        internal System.Windows.Forms.TreeView TView_UPSVar;
        internal System.Windows.Forms.GroupBox GB1;
        internal System.Windows.Forms.Label Lbl_D_Value;
        internal System.Windows.Forms.Label Lbl_V_Value;
        internal System.Windows.Forms.Label Lbl_N_Value;
        internal System.Windows.Forms.Label Lbl_D;
        internal System.Windows.Forms.Label Lbl_V;
        internal System.Windows.Forms.Label Lbl_Name;
        internal System.Windows.Forms.Button Btn_Reload;
        internal System.Windows.Forms.Button Btn_Close;
        internal System.Windows.Forms.Timer Timer_Update_List;
        internal System.Windows.Forms.Button Btn_Clip;
        internal System.Windows.Forms.ToolTip ToolTip;
        private System.Windows.Forms.Button Btn_Save;
    }
}
