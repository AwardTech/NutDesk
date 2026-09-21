using System.Windows.Forms;

namespace NutDesk
{
    partial class MainForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            this.NotifyIcon = new System.Windows.Forms.NotifyIcon(this.components);
            this.ContextMenu_Systray = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.Menu_Sys_Settings = new System.Windows.Forms.ToolStripMenuItem();
            this.Menu_Sys_Sep1 = new System.Windows.Forms.ToolStripSeparator();
            this.Menu_Sys_Update = new System.Windows.Forms.ToolStripMenuItem();
            this.Menu_Sys_Sep2 = new System.Windows.Forms.ToolStripSeparator();
            this.Menu_Sys_About = new System.Windows.Forms.ToolStripMenuItem();
            this.Menu_Sys_Sep3 = new System.Windows.Forms.ToolStripSeparator();
            this.Menu_Sys_Exit = new System.Windows.Forms.ToolStripMenuItem();
            this.Main_Menu = new System.Windows.Forms.MenuStrip();
            this.Menu_File = new System.Windows.Forms.ToolStripMenuItem();
            this.Menu_UPS_Var = new System.Windows.Forms.ToolStripMenuItem();
            this.ManageOldPrefsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.Menu_Quit = new System.Windows.Forms.ToolStripMenuItem();
            this.Menu_Connection = new System.Windows.Forms.ToolStripMenuItem();
            this.Menu_Persist = new System.Windows.Forms.ToolStripMenuItem();
            this.ToolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.Menu_Connect = new System.Windows.Forms.ToolStripMenuItem();
            this.Menu_Disconnect = new System.Windows.Forms.ToolStripMenuItem();
            this.Menu_Settings = new System.Windows.Forms.ToolStripMenuItem();
            this.Menu_Help = new System.Windows.Forms.ToolStripMenuItem();
            this.Menu_About = new System.Windows.Forms.ToolStripMenuItem();
            this.Menu_Help_Sep1 = new System.Windows.Forms.ToolStripSeparator();
            this.Menu_Update = new System.Windows.Forms.ToolStripMenuItem();
            this.GB_Status = new System.Windows.Forms.GroupBox();
            this.Lbl_VSerial = new System.Windows.Forms.Label();
            this.Lbl_VFirmware = new System.Windows.Forms.Label();
            this.Lbl_VName = new System.Windows.Forms.Label();
            this.Lbl_VMfr = new System.Windows.Forms.Label();
            this.Lbl_VRTime = new System.Windows.Forms.Label();
            this.Lbl_VOB = new System.Windows.Forms.Label();
            this.Lbl_VOLoad = new System.Windows.Forms.Label();
            this.Lbl_VBL = new System.Windows.Forms.Label();
            this.Lbl_VOL = new System.Windows.Forms.Label();
            this.Lbl_Firmware = new System.Windows.Forms.Label();
            this.Lbl_Serial = new System.Windows.Forms.Label();
            this.Lbl_Name = new System.Windows.Forms.Label();
            this.Lbl_Mfr = new System.Windows.Forms.Label();
            this.Lbl_RTime = new System.Windows.Forms.Label();
            this.Lbl_BL = new System.Windows.Forms.Label();
            this.Lbl_OLoad = new System.Windows.Forms.Label();
            this.Lbl_OB = new System.Windows.Forms.Label();
            this.Lbl_OL = new System.Windows.Forms.Label();
            this.GB_InV_Dial = new System.Windows.Forms.GroupBox();
            this.AG_InV = new NutDesk.Controls.UPSVarGauge();
            this.Lbl_InV_Dial = new System.Windows.Forms.Label();
            this.GB_OutV_Dial = new System.Windows.Forms.GroupBox();
            this.AG_OutV = new NutDesk.Controls.UPSVarGauge();
            this.Lbl_OutV_Dial = new System.Windows.Forms.Label();
            this.GB_BattCh_Dial = new System.Windows.Forms.GroupBox();
            this.PBox_Battery_State = new System.Windows.Forms.PictureBox();
            this.Lbl_BattCh_Dial = new System.Windows.Forms.Label();
            this.AG_BattCh = new NutDesk.Controls.UPSVarGauge();
            this.GB_Load_Dial = new System.Windows.Forms.GroupBox();
            this.AG_Load = new NutDesk.Controls.UPSVarGauge();
            this.Lbl_Load_Dial = new System.Windows.Forms.Label();
            this.GB_BattV_Dial = new System.Windows.Forms.GroupBox();
            this.AG_BattV = new NutDesk.Controls.UPSVarGauge();
            this.Lbl_BattV_Dial = new System.Windows.Forms.Label();
            this.GB_InF_Dial = new System.Windows.Forms.GroupBox();
            this.AG_InF = new NutDesk.Controls.UPSVarGauge();
            this.Lbl_InF_Dial = new System.Windows.Forms.Label();
            this.CB_CurrentLog = new System.Windows.Forms.ComboBox();
            this.ContextMenu_Systray.SuspendLayout();
            this.Main_Menu.SuspendLayout();
            this.GB_Status.SuspendLayout();
            this.GB_InV_Dial.SuspendLayout();
            this.GB_OutV_Dial.SuspendLayout();
            this.GB_BattCh_Dial.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.PBox_Battery_State)).BeginInit();
            this.GB_Load_Dial.SuspendLayout();
            this.GB_BattV_Dial.SuspendLayout();
            this.GB_InF_Dial.SuspendLayout();
            this.SuspendLayout();
            //
            // NotifyIcon
            //
            this.NotifyIcon.ContextMenuStrip = this.ContextMenu_Systray;
            resources.ApplyResources(this.NotifyIcon, "NotifyIcon");
            //
            // ContextMenu_Systray
            //
            this.ContextMenu_Systray.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { this.Menu_Sys_Settings, this.Menu_Sys_Sep1, this.Menu_Sys_Update, this.Menu_Sys_Sep2, this.Menu_Sys_About, this.Menu_Sys_Sep3, this.Menu_Sys_Exit });
            this.ContextMenu_Systray.Name = "ContextMenuStrip1";
            resources.ApplyResources(this.ContextMenu_Systray, "ContextMenu_Systray");
            //
            // Menu_Sys_Settings
            //
            this.Menu_Sys_Settings.Name = "Menu_Sys_Settings";
            resources.ApplyResources(this.Menu_Sys_Settings, "Menu_Sys_Settings");
            //
            // Menu_Sys_Sep1
            //
            this.Menu_Sys_Sep1.Name = "Menu_Sys_Sep1";
            resources.ApplyResources(this.Menu_Sys_Sep1, "Menu_Sys_Sep1");
            //
            // Menu_Sys_Update
            //
            this.Menu_Sys_Update.Name = "Menu_Sys_Update";
            resources.ApplyResources(this.Menu_Sys_Update, "Menu_Sys_Update");
            //
            // Menu_Sys_Sep2
            //
            this.Menu_Sys_Sep2.Name = "Menu_Sys_Sep2";
            resources.ApplyResources(this.Menu_Sys_Sep2, "Menu_Sys_Sep2");
            //
            // Menu_Sys_About
            //
            this.Menu_Sys_About.Name = "Menu_Sys_About";
            resources.ApplyResources(this.Menu_Sys_About, "Menu_Sys_About");
            //
            // Menu_Sys_Sep3
            //
            this.Menu_Sys_Sep3.Name = "Menu_Sys_Sep3";
            resources.ApplyResources(this.Menu_Sys_Sep3, "Menu_Sys_Sep3");
            //
            // Menu_Sys_Exit
            //
            this.Menu_Sys_Exit.Name = "Menu_Sys_Exit";
            resources.ApplyResources(this.Menu_Sys_Exit, "Menu_Sys_Exit");
            //
            // Main_Menu
            //
            resources.ApplyResources(this.Main_Menu, "Main_Menu");
            this.Main_Menu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { this.Menu_File, this.Menu_Connection, this.Menu_Settings, this.Menu_Help });
            this.Main_Menu.Name = "Main_Menu";
            //
            // Menu_File
            //
            this.Menu_File.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { this.Menu_UPS_Var, this.ManageOldPrefsToolStripMenuItem, this.Menu_Quit });
            this.Menu_File.Name = "Menu_File";
            resources.ApplyResources(this.Menu_File, "Menu_File");
            //
            // Menu_UPS_Var
            //
            resources.ApplyResources(this.Menu_UPS_Var, "Menu_UPS_Var");
            this.Menu_UPS_Var.Name = "Menu_UPS_Var";
            //
            // ManageOldPrefsToolStripMenuItem
            //
            resources.ApplyResources(this.ManageOldPrefsToolStripMenuItem, "ManageOldPrefsToolStripMenuItem");
            this.ManageOldPrefsToolStripMenuItem.Image = (System.Drawing.Image)NutDesk.Properties.Resources.regedit_exe_14_100_0;
            this.ManageOldPrefsToolStripMenuItem.Name = "ManageOldPrefsToolStripMenuItem";
            //
            // Menu_Quit
            //
            this.Menu_Quit.Name = "Menu_Quit";
            resources.ApplyResources(this.Menu_Quit, "Menu_Quit");
            //
            // Menu_Connection
            //
            this.Menu_Connection.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { this.Menu_Persist, this.ToolStripSeparator1, this.Menu_Connect, this.Menu_Disconnect });
            this.Menu_Connection.Name = "Menu_Connection";
            resources.ApplyResources(this.Menu_Connection, "Menu_Connection");
            //
            // Menu_Persist
            //
            this.Menu_Persist.Checked = NutDesk.Properties.Settings.Default.NUT_AutoReconnect;
            this.Menu_Persist.CheckOnClick = true;
            this.Menu_Persist.Image = (System.Drawing.Image)NutDesk.Properties.Resources.RepeatHS;
            this.Menu_Persist.Name = "Menu_Persist";
            resources.ApplyResources(this.Menu_Persist, "Menu_Persist");
            //
            // ToolStripSeparator1
            //
            this.ToolStripSeparator1.Name = "ToolStripSeparator1";
            resources.ApplyResources(this.ToolStripSeparator1, "ToolStripSeparator1");
            //
            // Menu_Connect
            //
            this.Menu_Connect.Image = (System.Drawing.Image)NutDesk.Properties.Resources.internetconnection;
            this.Menu_Connect.Name = "Menu_Connect";
            resources.ApplyResources(this.Menu_Connect, "Menu_Connect");
            //
            // Menu_Disconnect
            //
            resources.ApplyResources(this.Menu_Disconnect, "Menu_Disconnect");
            this.Menu_Disconnect.Image = (System.Drawing.Image)NutDesk.Properties.Resources.disconnect2;
            this.Menu_Disconnect.Name = "Menu_Disconnect";
            //
            // Menu_Settings
            //
            this.Menu_Settings.Name = "Menu_Settings";
            resources.ApplyResources(this.Menu_Settings, "Menu_Settings");
            //
            // Menu_Help
            //
            this.Menu_Help.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { this.Menu_About, this.Menu_Help_Sep1, this.Menu_Update });
            this.Menu_Help.Name = "Menu_Help";
            resources.ApplyResources(this.Menu_Help, "Menu_Help");
            //
            // Menu_About
            //
            this.Menu_About.Name = "Menu_About";
            resources.ApplyResources(this.Menu_About, "Menu_About");
            //
            // Menu_Help_Sep1
            //
            this.Menu_Help_Sep1.Name = "Menu_Help_Sep1";
            resources.ApplyResources(this.Menu_Help_Sep1, "Menu_Help_Sep1");
            //
            // Menu_Update
            //
            this.Menu_Update.Name = "Menu_Update";
            resources.ApplyResources(this.Menu_Update, "Menu_Update");
            //
            // GB_Status
            //
            resources.ApplyResources(this.GB_Status, "GB_Status");
            this.GB_Status.Controls.Add(this.Lbl_VSerial);
            this.GB_Status.Controls.Add(this.Lbl_VFirmware);
            this.GB_Status.Controls.Add(this.Lbl_VName);
            this.GB_Status.Controls.Add(this.Lbl_VMfr);
            this.GB_Status.Controls.Add(this.Lbl_VRTime);
            this.GB_Status.Controls.Add(this.Lbl_VOB);
            this.GB_Status.Controls.Add(this.Lbl_VOLoad);
            this.GB_Status.Controls.Add(this.Lbl_VBL);
            this.GB_Status.Controls.Add(this.Lbl_VOL);
            this.GB_Status.Controls.Add(this.Lbl_Firmware);
            this.GB_Status.Controls.Add(this.Lbl_Serial);
            this.GB_Status.Controls.Add(this.Lbl_Name);
            this.GB_Status.Controls.Add(this.Lbl_Mfr);
            this.GB_Status.Controls.Add(this.Lbl_RTime);
            this.GB_Status.Controls.Add(this.Lbl_BL);
            this.GB_Status.Controls.Add(this.Lbl_OLoad);
            this.GB_Status.Controls.Add(this.Lbl_OB);
            this.GB_Status.Controls.Add(this.Lbl_OL);
            this.GB_Status.Name = "GB_Status";
            this.GB_Status.TabStop = false;
            //
            // Lbl_VSerial
            //
            this.Lbl_VSerial.CausesValidation = false;
            resources.ApplyResources(this.Lbl_VSerial, "Lbl_VSerial");
            this.Lbl_VSerial.Name = "Lbl_VSerial";
            //
            // Lbl_VFirmware
            //
            this.Lbl_VFirmware.CausesValidation = false;
            resources.ApplyResources(this.Lbl_VFirmware, "Lbl_VFirmware");
            this.Lbl_VFirmware.Name = "Lbl_VFirmware";
            this.Lbl_VFirmware.UseCompatibleTextRendering = true;
            //
            // Lbl_VName
            //
            this.Lbl_VName.CausesValidation = false;
            resources.ApplyResources(this.Lbl_VName, "Lbl_VName");
            this.Lbl_VName.Name = "Lbl_VName";
            //
            // Lbl_VMfr
            //
            this.Lbl_VMfr.CausesValidation = false;
            resources.ApplyResources(this.Lbl_VMfr, "Lbl_VMfr");
            this.Lbl_VMfr.Name = "Lbl_VMfr";
            //
            // Lbl_VRTime
            //
            this.Lbl_VRTime.CausesValidation = false;
            resources.ApplyResources(this.Lbl_VRTime, "Lbl_VRTime");
            this.Lbl_VRTime.Name = "Lbl_VRTime";
            //
            // Lbl_VOB
            //
            this.Lbl_VOB.BackColor = System.Drawing.Color.White;
            this.Lbl_VOB.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.Lbl_VOB.CausesValidation = false;
            resources.ApplyResources(this.Lbl_VOB, "Lbl_VOB");
            this.Lbl_VOB.Name = "Lbl_VOB";
            //
            // Lbl_VOLoad
            //
            this.Lbl_VOLoad.BackColor = System.Drawing.Color.White;
            this.Lbl_VOLoad.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.Lbl_VOLoad.CausesValidation = false;
            resources.ApplyResources(this.Lbl_VOLoad, "Lbl_VOLoad");
            this.Lbl_VOLoad.Name = "Lbl_VOLoad";
            //
            // Lbl_VBL
            //
            this.Lbl_VBL.BackColor = System.Drawing.Color.White;
            this.Lbl_VBL.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.Lbl_VBL.CausesValidation = false;
            resources.ApplyResources(this.Lbl_VBL, "Lbl_VBL");
            this.Lbl_VBL.Name = "Lbl_VBL";
            //
            // Lbl_VOL
            //
            this.Lbl_VOL.BackColor = System.Drawing.Color.White;
            this.Lbl_VOL.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.Lbl_VOL.CausesValidation = false;
            resources.ApplyResources(this.Lbl_VOL, "Lbl_VOL");
            this.Lbl_VOL.Name = "Lbl_VOL";
            //
            // Lbl_Firmware
            //
            this.Lbl_Firmware.CausesValidation = false;
            resources.ApplyResources(this.Lbl_Firmware, "Lbl_Firmware");
            this.Lbl_Firmware.Name = "Lbl_Firmware";
            this.Lbl_Firmware.UseCompatibleTextRendering = true;
            //
            // Lbl_Serial
            //
            this.Lbl_Serial.CausesValidation = false;
            resources.ApplyResources(this.Lbl_Serial, "Lbl_Serial");
            this.Lbl_Serial.Name = "Lbl_Serial";
            //
            // Lbl_Name
            //
            this.Lbl_Name.CausesValidation = false;
            resources.ApplyResources(this.Lbl_Name, "Lbl_Name");
            this.Lbl_Name.Name = "Lbl_Name";
            //
            // Lbl_Mfr
            //
            this.Lbl_Mfr.CausesValidation = false;
            resources.ApplyResources(this.Lbl_Mfr, "Lbl_Mfr");
            this.Lbl_Mfr.Name = "Lbl_Mfr";
            //
            // Lbl_RTime
            //
            this.Lbl_RTime.CausesValidation = false;
            resources.ApplyResources(this.Lbl_RTime, "Lbl_RTime");
            this.Lbl_RTime.Name = "Lbl_RTime";
            //
            // Lbl_BL
            //
            this.Lbl_BL.CausesValidation = false;
            resources.ApplyResources(this.Lbl_BL, "Lbl_BL");
            this.Lbl_BL.Name = "Lbl_BL";
            //
            // Lbl_OLoad
            //
            this.Lbl_OLoad.CausesValidation = false;
            resources.ApplyResources(this.Lbl_OLoad, "Lbl_OLoad");
            this.Lbl_OLoad.Name = "Lbl_OLoad";
            //
            // Lbl_OB
            //
            this.Lbl_OB.CausesValidation = false;
            resources.ApplyResources(this.Lbl_OB, "Lbl_OB");
            this.Lbl_OB.Name = "Lbl_OB";
            //
            // Lbl_OL
            //
            this.Lbl_OL.CausesValidation = false;
            resources.ApplyResources(this.Lbl_OL, "Lbl_OL");
            this.Lbl_OL.Name = "Lbl_OL";
            //
            // GB_InV_Dial
            //
            resources.ApplyResources(this.GB_InV_Dial, "GB_InV_Dial");
            this.GB_InV_Dial.Controls.Add(this.AG_InV);
            this.GB_InV_Dial.Controls.Add(this.Lbl_InV_Dial);
            this.GB_InV_Dial.Name = "GB_InV_Dial";
            this.GB_InV_Dial.TabStop = false;
            //
            // AG_InV
            //
            this.AG_InV.BaseArcRadius = 45;
            this.AG_InV.BaseArcWidth = 5;
            this.AG_InV.GradientOrientation = NutDesk.Controls.UPSVarGauge.GradientOrientationEnum.BottomToTop;
            this.AG_InV.GradientType = NutDesk.Controls.UPSVarGauge.GradientTypeEnum.RedGreen;
            resources.ApplyResources(this.AG_InV, "AG_InV");
            this.AG_InV.MaxValue = 100;
            this.AG_InV.MinValue = 0;
            this.AG_InV.Name = "AG_InV";
            this.AG_InV.NeedleRadius = 32;
            this.AG_InV.ScaleLinesInterInnerRadius = 40;
            this.AG_InV.ScaleLinesInterOuterRadius = 48;
            this.AG_InV.ScaleLinesMajorInnerRadius = 40;
            this.AG_InV.ScaleLinesMajorOuterRadius = 48;
            this.AG_InV.ScaleLinesMinorInnerRadius = 42;
            this.AG_InV.ScaleLinesMinorOuterRadius = 48;
            this.AG_InV.ScaleNumbersFormat = null;
            this.AG_InV.ScaleNumbersRadius = 60;
            this.AG_InV.UnitValue1 = NutDesk.Controls.UPSVarGauge.UnitValueEnum.Volts;
            this.AG_InV.UnitValue2 = NutDesk.Controls.UPSVarGauge.UnitValueEnum.None;
            this.AG_InV.Value = 0f;
            this.AG_InV.Value1 = 0f;
            this.AG_InV.Value2 = 0f;
            //
            // Lbl_InV_Dial
            //
            resources.ApplyResources(this.Lbl_InV_Dial, "Lbl_InV_Dial");
            this.Lbl_InV_Dial.Name = "Lbl_InV_Dial";
            //
            // GB_OutV_Dial
            //
            resources.ApplyResources(this.GB_OutV_Dial, "GB_OutV_Dial");
            this.GB_OutV_Dial.Controls.Add(this.AG_OutV);
            this.GB_OutV_Dial.Controls.Add(this.Lbl_OutV_Dial);
            this.GB_OutV_Dial.Name = "GB_OutV_Dial";
            this.GB_OutV_Dial.TabStop = false;
            //
            // AG_OutV
            //
            this.AG_OutV.BaseArcRadius = 45;
            this.AG_OutV.BaseArcWidth = 5;
            this.AG_OutV.GradientOrientation = NutDesk.Controls.UPSVarGauge.GradientOrientationEnum.BottomToTop;
            this.AG_OutV.GradientType = NutDesk.Controls.UPSVarGauge.GradientTypeEnum.RedGreen;
            resources.ApplyResources(this.AG_OutV, "AG_OutV");
            this.AG_OutV.MaxValue = 100;
            this.AG_OutV.MinValue = 0;
            this.AG_OutV.Name = "AG_OutV";
            this.AG_OutV.NeedleRadius = 32;
            this.AG_OutV.ScaleLinesInterInnerRadius = 40;
            this.AG_OutV.ScaleLinesInterOuterRadius = 48;
            this.AG_OutV.ScaleLinesMajorInnerRadius = 40;
            this.AG_OutV.ScaleLinesMajorOuterRadius = 48;
            this.AG_OutV.ScaleLinesMinorInnerRadius = 42;
            this.AG_OutV.ScaleLinesMinorOuterRadius = 48;
            this.AG_OutV.ScaleNumbersFormat = null;
            this.AG_OutV.ScaleNumbersRadius = 60;
            this.AG_OutV.UnitValue1 = NutDesk.Controls.UPSVarGauge.UnitValueEnum.Volts;
            this.AG_OutV.UnitValue2 = NutDesk.Controls.UPSVarGauge.UnitValueEnum.None;
            this.AG_OutV.Value = 0f;
            this.AG_OutV.Value1 = 0f;
            this.AG_OutV.Value2 = 0f;
            //
            // Lbl_OutV_Dial
            //
            resources.ApplyResources(this.Lbl_OutV_Dial, "Lbl_OutV_Dial");
            this.Lbl_OutV_Dial.Name = "Lbl_OutV_Dial";
            //
            // GB_BattCh_Dial
            //
            resources.ApplyResources(this.GB_BattCh_Dial, "GB_BattCh_Dial");
            this.GB_BattCh_Dial.Controls.Add(this.PBox_Battery_State);
            this.GB_BattCh_Dial.Controls.Add(this.Lbl_BattCh_Dial);
            this.GB_BattCh_Dial.Controls.Add(this.AG_BattCh);
            this.GB_BattCh_Dial.Name = "GB_BattCh_Dial";
            this.GB_BattCh_Dial.TabStop = false;
            //
            // PBox_Battery_State
            //
            resources.ApplyResources(this.PBox_Battery_State, "PBox_Battery_State");
            this.PBox_Battery_State.Name = "PBox_Battery_State";
            this.PBox_Battery_State.TabStop = false;
            //
            // Lbl_BattCh_Dial
            //
            resources.ApplyResources(this.Lbl_BattCh_Dial, "Lbl_BattCh_Dial");
            this.Lbl_BattCh_Dial.Name = "Lbl_BattCh_Dial";
            //
            // AG_BattCh
            //
            this.AG_BattCh.BaseArcRadius = 45;
            this.AG_BattCh.BaseArcWidth = 5;
            this.AG_BattCh.GradientOrientation = NutDesk.Controls.UPSVarGauge.GradientOrientationEnum.LeftToRight;
            this.AG_BattCh.GradientType = NutDesk.Controls.UPSVarGauge.GradientTypeEnum.RedGreen;
            resources.ApplyResources(this.AG_BattCh, "AG_BattCh");
            this.AG_BattCh.MaxValue = 100;
            this.AG_BattCh.MinValue = 0;
            this.AG_BattCh.Name = "AG_BattCh";
            this.AG_BattCh.NeedleRadius = 32;
            this.AG_BattCh.ScaleLinesInterInnerRadius = 40;
            this.AG_BattCh.ScaleLinesInterOuterRadius = 48;
            this.AG_BattCh.ScaleLinesMajorInnerRadius = 40;
            this.AG_BattCh.ScaleLinesMajorOuterRadius = 48;
            this.AG_BattCh.ScaleLinesMinorInnerRadius = 42;
            this.AG_BattCh.ScaleLinesMinorOuterRadius = 48;
            this.AG_BattCh.ScaleNumbersFormat = null;
            this.AG_BattCh.ScaleNumbersRadius = 60;
            this.AG_BattCh.UnitValue1 = NutDesk.Controls.UPSVarGauge.UnitValueEnum.Percent;
            this.AG_BattCh.UnitValue2 = NutDesk.Controls.UPSVarGauge.UnitValueEnum.None;
            this.AG_BattCh.Value = 0f;
            this.AG_BattCh.Value1 = 0f;
            this.AG_BattCh.Value2 = 0f;
            //
            // GB_Load_Dial
            //
            resources.ApplyResources(this.GB_Load_Dial, "GB_Load_Dial");
            this.GB_Load_Dial.Controls.Add(this.AG_Load);
            this.GB_Load_Dial.Controls.Add(this.Lbl_Load_Dial);
            this.GB_Load_Dial.Name = "GB_Load_Dial";
            this.GB_Load_Dial.TabStop = false;
            //
            // AG_Load
            //
            this.AG_Load.BaseArcRadius = 45;
            this.AG_Load.BaseArcWidth = 5;
            this.AG_Load.GradientOrientation = NutDesk.Controls.UPSVarGauge.GradientOrientationEnum.RightToLeft;
            this.AG_Load.GradientType = NutDesk.Controls.UPSVarGauge.GradientTypeEnum.RedGreen;
            resources.ApplyResources(this.AG_Load, "AG_Load");
            this.AG_Load.MaxValue = 100;
            this.AG_Load.MinValue = 0;
            this.AG_Load.Name = "AG_Load";
            this.AG_Load.NeedleRadius = 32;
            this.AG_Load.ScaleLinesInterInnerRadius = 40;
            this.AG_Load.ScaleLinesInterOuterRadius = 48;
            this.AG_Load.ScaleLinesMajorInnerRadius = 40;
            this.AG_Load.ScaleLinesMajorOuterRadius = 48;
            this.AG_Load.ScaleLinesMinorInnerRadius = 42;
            this.AG_Load.ScaleLinesMinorOuterRadius = 48;
            this.AG_Load.ScaleNumbersFormat = null;
            this.AG_Load.ScaleNumbersRadius = 60;
            this.AG_Load.UnitValue1 = NutDesk.Controls.UPSVarGauge.UnitValueEnum.Percent;
            this.AG_Load.UnitValue2 = NutDesk.Controls.UPSVarGauge.UnitValueEnum.Watts;
            this.AG_Load.Value = 0f;
            this.AG_Load.Value1 = 0f;
            this.AG_Load.Value2 = 0f;
            //
            // Lbl_Load_Dial
            //
            resources.ApplyResources(this.Lbl_Load_Dial, "Lbl_Load_Dial");
            this.Lbl_Load_Dial.Name = "Lbl_Load_Dial";
            //
            // GB_BattV_Dial
            //
            resources.ApplyResources(this.GB_BattV_Dial, "GB_BattV_Dial");
            this.GB_BattV_Dial.Controls.Add(this.AG_BattV);
            this.GB_BattV_Dial.Controls.Add(this.Lbl_BattV_Dial);
            this.GB_BattV_Dial.Name = "GB_BattV_Dial";
            this.GB_BattV_Dial.TabStop = false;
            //
            // AG_BattV
            //
            this.AG_BattV.BaseArcRadius = 45;
            this.AG_BattV.BaseArcWidth = 5;
            this.AG_BattV.GradientOrientation = NutDesk.Controls.UPSVarGauge.GradientOrientationEnum.BottomToTop;
            this.AG_BattV.GradientType = NutDesk.Controls.UPSVarGauge.GradientTypeEnum.RedGreen;
            resources.ApplyResources(this.AG_BattV, "AG_BattV");
            this.AG_BattV.MaxValue = 100;
            this.AG_BattV.MinValue = 0;
            this.AG_BattV.Name = "AG_BattV";
            this.AG_BattV.NeedleRadius = 32;
            this.AG_BattV.ScaleLinesInterInnerRadius = 40;
            this.AG_BattV.ScaleLinesInterOuterRadius = 48;
            this.AG_BattV.ScaleLinesMajorInnerRadius = 40;
            this.AG_BattV.ScaleLinesMajorOuterRadius = 48;
            this.AG_BattV.ScaleLinesMinorInnerRadius = 42;
            this.AG_BattV.ScaleLinesMinorOuterRadius = 48;
            this.AG_BattV.ScaleNumbersFormat = null;
            this.AG_BattV.ScaleNumbersRadius = 60;
            this.AG_BattV.UnitValue1 = NutDesk.Controls.UPSVarGauge.UnitValueEnum.Volts;
            this.AG_BattV.UnitValue2 = NutDesk.Controls.UPSVarGauge.UnitValueEnum.None;
            this.AG_BattV.Value = 0f;
            this.AG_BattV.Value1 = 0f;
            this.AG_BattV.Value2 = 0f;
            //
            // Lbl_BattV_Dial
            //
            resources.ApplyResources(this.Lbl_BattV_Dial, "Lbl_BattV_Dial");
            this.Lbl_BattV_Dial.Name = "Lbl_BattV_Dial";
            //
            // GB_InF_Dial
            //
            resources.ApplyResources(this.GB_InF_Dial, "GB_InF_Dial");
            this.GB_InF_Dial.Controls.Add(this.AG_InF);
            this.GB_InF_Dial.Controls.Add(this.Lbl_InF_Dial);
            this.GB_InF_Dial.Name = "GB_InF_Dial";
            this.GB_InF_Dial.TabStop = false;
            //
            // AG_InF
            //
            this.AG_InF.BaseArcRadius = 45;
            this.AG_InF.BaseArcWidth = 5;
            this.AG_InF.GradientOrientation = NutDesk.Controls.UPSVarGauge.GradientOrientationEnum.BottomToTop;
            this.AG_InF.GradientType = NutDesk.Controls.UPSVarGauge.GradientTypeEnum.RedGreen;
            resources.ApplyResources(this.AG_InF, "AG_InF");
            this.AG_InF.MaxValue = 100;
            this.AG_InF.MinValue = 0;
            this.AG_InF.Name = "AG_InF";
            this.AG_InF.NeedleRadius = 32;
            this.AG_InF.ScaleLinesInterInnerRadius = 40;
            this.AG_InF.ScaleLinesInterOuterRadius = 48;
            this.AG_InF.ScaleLinesMajorInnerRadius = 40;
            this.AG_InF.ScaleLinesMajorOuterRadius = 48;
            this.AG_InF.ScaleLinesMinorInnerRadius = 42;
            this.AG_InF.ScaleLinesMinorOuterRadius = 48;
            this.AG_InF.ScaleNumbersFormat = null;
            this.AG_InF.ScaleNumbersRadius = 60;
            this.AG_InF.UnitValue1 = NutDesk.Controls.UPSVarGauge.UnitValueEnum.Hertz;
            this.AG_InF.UnitValue2 = NutDesk.Controls.UPSVarGauge.UnitValueEnum.None;
            this.AG_InF.Value = 0f;
            this.AG_InF.Value1 = 0f;
            this.AG_InF.Value2 = 0f;
            //
            // Lbl_InF_Dial
            //
            resources.ApplyResources(this.Lbl_InF_Dial, "Lbl_InF_Dial");
            this.Lbl_InF_Dial.Name = "Lbl_InF_Dial";
            //
            // CB_CurrentLog
            //
            this.CB_CurrentLog.CausesValidation = false;
            this.CB_CurrentLog.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            resources.ApplyResources(this.CB_CurrentLog, "CB_CurrentLog");
            this.CB_CurrentLog.Name = "CB_CurrentLog";
            //
            // MainForm
            //
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.AutoValidate = System.Windows.Forms.AutoValidate.Disable;
            resources.ApplyResources(this, "$this");
            this.Controls.Add(this.CB_CurrentLog);
            this.Controls.Add(this.GB_InF_Dial);
            this.Controls.Add(this.GB_InV_Dial);
            this.Controls.Add(this.GB_BattV_Dial);
            this.Controls.Add(this.GB_Load_Dial);
            this.Controls.Add(this.GB_OutV_Dial);
            this.Controls.Add(this.GB_Status);
            this.Controls.Add(this.GB_BattCh_Dial);
            this.Controls.Add(this.Main_Menu);
            this.DoubleBuffered = true;
            this.MainMenuStrip = this.Main_Menu;
            this.MaximizeBox = false;
            this.Name = "MainForm";
            this.ContextMenu_Systray.ResumeLayout(false);
            this.Main_Menu.ResumeLayout(false);
            this.Main_Menu.PerformLayout();
            this.GB_Status.ResumeLayout(false);
            this.GB_InV_Dial.ResumeLayout(false);
            this.GB_InV_Dial.PerformLayout();
            this.GB_OutV_Dial.ResumeLayout(false);
            this.GB_OutV_Dial.PerformLayout();
            this.GB_BattCh_Dial.ResumeLayout(false);
            this.GB_BattCh_Dial.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.PBox_Battery_State)).EndInit();
            this.GB_Load_Dial.ResumeLayout(false);
            this.GB_Load_Dial.PerformLayout();
            this.GB_BattV_Dial.ResumeLayout(false);
            this.GB_BattV_Dial.PerformLayout();
            this.GB_InF_Dial.ResumeLayout(false);
            this.GB_InF_Dial.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        internal NotifyIcon NotifyIcon;
        internal MenuStrip Main_Menu;
        internal ToolStripMenuItem Menu_File;
        internal ToolStripMenuItem Menu_UPS_Var;
        internal ToolStripMenuItem Menu_Quit;
        internal ToolStripMenuItem Menu_Connection;
        internal ToolStripMenuItem Menu_Settings;
        internal ToolStripMenuItem Menu_Help;
        internal ToolStripMenuItem Menu_About;
        internal ToolStripSeparator Menu_Help_Sep1;
        internal ToolStripMenuItem Menu_Update;
        internal ToolStripMenuItem Menu_Connect;
        internal ToolStripMenuItem Menu_Disconnect;
        internal ContextMenuStrip ContextMenu_Systray;
        internal ToolStripMenuItem Menu_Sys_Settings;
        internal ToolStripSeparator Menu_Sys_Sep1;
        internal ToolStripMenuItem Menu_Sys_Update;
        internal ToolStripSeparator Menu_Sys_Sep2;
        internal ToolStripMenuItem Menu_Sys_About;
        internal ToolStripSeparator Menu_Sys_Sep3;
        internal ToolStripMenuItem Menu_Sys_Exit;
        internal GroupBox GB_Status;
        internal Label Lbl_VSerial;
        internal Label Lbl_VFirmware;
        internal Label Lbl_VName;
        internal Label Lbl_VMfr;
        internal Label Lbl_VRTime;
        internal Label Lbl_VOB;
        internal Label Lbl_VOLoad;
        internal Label Lbl_VBL;
        internal Label Lbl_VOL;
        internal Label Lbl_Firmware;
        internal Label Lbl_Serial;
        internal Label Lbl_Name;
        internal Label Lbl_Mfr;
        internal Label Lbl_RTime;
        internal Label Lbl_BL;
        internal Label Lbl_OLoad;
        internal Label Lbl_OB;
        internal Label Lbl_OL;
        internal GroupBox GB_BattCh_Dial;
        internal Label Lbl_BattCh_Dial;
        internal GroupBox GB_OutV_Dial;
        internal Label Lbl_OutV_Dial;
        internal GroupBox GB_Load_Dial;
        internal Label Lbl_Load_Dial;
        internal GroupBox GB_BattV_Dial;
        internal Label Lbl_BattV_Dial;
        internal GroupBox GB_InV_Dial;
        internal Label Lbl_InV_Dial;
        internal NutDesk.Controls.UPSVarGauge AG_InV;
        internal NutDesk.Controls.UPSVarGauge AG_OutV;
        internal NutDesk.Controls.UPSVarGauge AG_BattCh;
        internal NutDesk.Controls.UPSVarGauge AG_Load;
        internal NutDesk.Controls.UPSVarGauge AG_BattV;
        internal GroupBox GB_InF_Dial;
        internal NutDesk.Controls.UPSVarGauge AG_InF;
        internal Label Lbl_InF_Dial;
        internal PictureBox PBox_Battery_State;
        internal ToolStripMenuItem ManageOldPrefsToolStripMenuItem;
        internal ToolStripSeparator ToolStripSeparator1;
        internal ToolStripMenuItem Menu_Persist;
        internal ComboBox CB_CurrentLog;
    }
}
