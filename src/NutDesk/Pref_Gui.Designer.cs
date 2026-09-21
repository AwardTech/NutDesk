namespace NutDesk
{
    partial class Pref_Gui
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Pref_Gui));
            this.TabControl_Options = new System.Windows.Forms.TabControl();
            this.Tab_Connexion = new System.Windows.Forms.TabPage();
            this.pollingIntervalUnitLabel = new System.Windows.Forms.Label();
            this.pollingIntervalValue = new System.Windows.Forms.NumericUpDown();
            this.Tb_Pwd_Nut = new System.Windows.Forms.TextBox();
            this.Tb_Login_Nut = new System.Windows.Forms.TextBox();
            this.Label3 = new System.Windows.Forms.Label();
            this.Lbl_Pwd_Nut = new System.Windows.Forms.Label();
            this.Lbl_Login_Nut = new System.Windows.Forms.Label();
            this.Tb_UPS_Name = new System.Windows.Forms.TextBox();
            this.Tb_Port = new System.Windows.Forms.TextBox();
            this.Tb_Server_IP = new System.Windows.Forms.TextBox();
            this.Cb_Reconnect = new System.Windows.Forms.CheckBox();
            this.Lbl_Delay_Com = new System.Windows.Forms.Label();
            this.Lbl_Name_UPS = new System.Windows.Forms.Label();
            this.Lbl_Port = new System.Windows.Forms.Label();
            this.Lbl_Server_IP = new System.Windows.Forms.Label();
            this.Tab_Calibrage = new System.Windows.Forms.TabPage();
            this.Cbx_Freq_Input = new System.Windows.Forms.ComboBox();
            this.Tb_BattV_Max = new System.Windows.Forms.TextBox();
            this.Tb_OutV_Max = new System.Windows.Forms.TextBox();
            this.Tb_InF_Max = new System.Windows.Forms.TextBox();
            this.Tb_BattV_Min = new System.Windows.Forms.TextBox();
            this.Tb_OutV_Min = new System.Windows.Forms.TextBox();
            this.Tb_InF_Min = new System.Windows.Forms.TextBox();
            this.Tb_InV_Max = new System.Windows.Forms.TextBox();
            this.Tb_InV_Min = new System.Windows.Forms.TextBox();
            this.Lbl_Maxi = new System.Windows.Forms.Label();
            this.Lbl_Mini = new System.Windows.Forms.Label();
            this.Lbl_BattV = new System.Windows.Forms.Label();
            this.Lbl_LoadUPS = new System.Windows.Forms.Label();
            this.Lbl_OutputV = new System.Windows.Forms.Label();
            this.Lbl_InputF = new System.Windows.Forms.Label();
            this.Lbl_PowerF = new System.Windows.Forms.Label();
            this.Lbl_InputV = new System.Windows.Forms.Label();
            this.Tab_Miscellanous = new System.Windows.Forms.TabPage();
            this.Cbx_LogLevel = new System.Windows.Forms.ComboBox();
            this.Btn_DeleteLog = new System.Windows.Forms.Button();
            this.Btn_ViewLog = new System.Windows.Forms.Button();
            this.Lbl_LevelLog = new System.Windows.Forms.Label();
            this.CB_Use_Logfile = new System.Windows.Forms.CheckBox();
            this.CB_Start_W_Win = new System.Windows.Forms.CheckBox();
            this.CB_Close_Tray = new System.Windows.Forms.CheckBox();
            this.CB_Start_Mini = new System.Windows.Forms.CheckBox();
            this.CB_Systray = new System.Windows.Forms.CheckBox();
            this.Tab_Shutdown = new System.Windows.Forms.TabPage();
            this.CB_Follow_FSD = new System.Windows.Forms.CheckBox();
            this.Lbl_Percent = new System.Windows.Forms.Label();
            this.Tb_GraceTime = new System.Windows.Forms.TextBox();
            this.Cb_ExtendTime = new System.Windows.Forms.CheckBox();
            this.Tb_Delay_Stop = new System.Windows.Forms.TextBox();
            this.Cbx_TypeStop = new System.Windows.Forms.ComboBox();
            this.Cb_ImmediateStop = new System.Windows.Forms.CheckBox();
            this.Tb_BattLimit_Time = new System.Windows.Forms.TextBox();
            this.Tb_BattLimit_Load = new System.Windows.Forms.TextBox();
            this.Lbl_GraceTime = new System.Windows.Forms.Label();
            this.Lbl_Delay_Stop = new System.Windows.Forms.Label();
            this.Lbl_StopType = new System.Windows.Forms.Label();
            this.Lbl_BattLimit_Time = new System.Windows.Forms.Label();
            this.Lbl_BattLimit_Load = new System.Windows.Forms.Label();
            this.Tab_Update = new System.Windows.Forms.TabPage();
            this.Cbx_Branch_Update = new System.Windows.Forms.ComboBox();
            this.Cbx_Delay_Verif = new System.Windows.Forms.ComboBox();
            this.Lbl_Branch_Update = new System.Windows.Forms.Label();
            this.Lbl_Delay_Verif = new System.Windows.Forms.Label();
            this.Cb_Update_At_Start = new System.Windows.Forms.CheckBox();
            this.Btn_Ok = new System.Windows.Forms.Button();
            this.Btn_Apply = new System.Windows.Forms.Button();
            this.Btn_Cancel = new System.Windows.Forms.Button();
            this.Pref_TlTip = new System.Windows.Forms.ToolTip(this.components);
            this.TabControl_Options.SuspendLayout();
            this.Tab_Connexion.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)this.pollingIntervalValue).BeginInit();
            this.Tab_Calibrage.SuspendLayout();
            this.Tab_Miscellanous.SuspendLayout();
            this.Tab_Shutdown.SuspendLayout();
            this.Tab_Update.SuspendLayout();
            this.SuspendLayout();
            //
            // TabControl_Options
            //
            this.TabControl_Options.Controls.Add(this.Tab_Connexion);
            this.TabControl_Options.Controls.Add(this.Tab_Calibrage);
            this.TabControl_Options.Controls.Add(this.Tab_Miscellanous);
            this.TabControl_Options.Controls.Add(this.Tab_Shutdown);
            this.TabControl_Options.Controls.Add(this.Tab_Update);
            resources.ApplyResources(this.TabControl_Options, "TabControl_Options");
            this.TabControl_Options.Name = "TabControl_Options";
            this.TabControl_Options.SelectedIndex = 0;
            //
            // Tab_Connexion
            //
            this.Tab_Connexion.Controls.Add(this.pollingIntervalUnitLabel);
            this.Tab_Connexion.Controls.Add(this.pollingIntervalValue);
            this.Tab_Connexion.Controls.Add(this.Tb_Pwd_Nut);
            this.Tab_Connexion.Controls.Add(this.Tb_Login_Nut);
            this.Tab_Connexion.Controls.Add(this.Label3);
            this.Tab_Connexion.Controls.Add(this.Lbl_Pwd_Nut);
            this.Tab_Connexion.Controls.Add(this.Lbl_Login_Nut);
            this.Tab_Connexion.Controls.Add(this.Tb_UPS_Name);
            this.Tab_Connexion.Controls.Add(this.Tb_Port);
            this.Tab_Connexion.Controls.Add(this.Tb_Server_IP);
            this.Tab_Connexion.Controls.Add(this.Cb_Reconnect);
            this.Tab_Connexion.Controls.Add(this.Lbl_Delay_Com);
            this.Tab_Connexion.Controls.Add(this.Lbl_Name_UPS);
            this.Tab_Connexion.Controls.Add(this.Lbl_Port);
            this.Tab_Connexion.Controls.Add(this.Lbl_Server_IP);
            resources.ApplyResources(this.Tab_Connexion, "Tab_Connexion");
            this.Tab_Connexion.Name = "Tab_Connexion";
            this.Tab_Connexion.UseVisualStyleBackColor = true;
            //
            // pollingIntervalUnitLabel
            //
            resources.ApplyResources(this.pollingIntervalUnitLabel, "pollingIntervalUnitLabel");
            this.pollingIntervalUnitLabel.Name = "pollingIntervalUnitLabel";
            //
            // pollingIntervalValue
            //
            this.pollingIntervalValue.DecimalPlaces = 1;
            this.pollingIntervalValue.Increment = new decimal(new int[] { 1, 0, 0, 65536 });
            resources.ApplyResources(this.pollingIntervalValue, "pollingIntervalValue");
            this.pollingIntervalValue.Minimum = new decimal(new int[] { 1, 0, 0, 65536 });
            this.pollingIntervalValue.Name = "pollingIntervalValue";
            this.Pref_TlTip.SetToolTip(this.pollingIntervalValue, resources.GetString("pollingIntervalValue.ToolTip"));
            this.pollingIntervalValue.Value = new decimal(new int[] { 1, 0, 0, 65536 });
            //
            // Tb_Pwd_Nut
            //
            resources.ApplyResources(this.Tb_Pwd_Nut, "Tb_Pwd_Nut");
            this.Tb_Pwd_Nut.Name = "Tb_Pwd_Nut";
            this.Pref_TlTip.SetToolTip(this.Tb_Pwd_Nut, resources.GetString("Tb_Pwd_Nut.ToolTip"));
            this.Tb_Pwd_Nut.UseSystemPasswordChar = true;
            //
            // Tb_Login_Nut
            //
            resources.ApplyResources(this.Tb_Login_Nut, "Tb_Login_Nut");
            this.Tb_Login_Nut.Name = "Tb_Login_Nut";
            this.Pref_TlTip.SetToolTip(this.Tb_Login_Nut, resources.GetString("Tb_Login_Nut.ToolTip"));
            //
            // Label3
            //
            resources.ApplyResources(this.Label3, "Label3");
            this.Label3.Name = "Label3";
            //
            // Lbl_Pwd_Nut
            //
            resources.ApplyResources(this.Lbl_Pwd_Nut, "Lbl_Pwd_Nut");
            this.Lbl_Pwd_Nut.Name = "Lbl_Pwd_Nut";
            //
            // Lbl_Login_Nut
            //
            resources.ApplyResources(this.Lbl_Login_Nut, "Lbl_Login_Nut");
            this.Lbl_Login_Nut.Name = "Lbl_Login_Nut";
            //
            // Tb_UPS_Name
            //
            resources.ApplyResources(this.Tb_UPS_Name, "Tb_UPS_Name");
            this.Tb_UPS_Name.Name = "Tb_UPS_Name";
            this.Pref_TlTip.SetToolTip(this.Tb_UPS_Name, resources.GetString("Tb_UPS_Name.ToolTip"));
            //
            // Tb_Port
            //
            resources.ApplyResources(this.Tb_Port, "Tb_Port");
            this.Tb_Port.Name = "Tb_Port";
            this.Pref_TlTip.SetToolTip(this.Tb_Port, resources.GetString("Tb_Port.ToolTip"));
            //
            // Tb_Server_IP
            //
            resources.ApplyResources(this.Tb_Server_IP, "Tb_Server_IP");
            this.Tb_Server_IP.Name = "Tb_Server_IP";
            this.Pref_TlTip.SetToolTip(this.Tb_Server_IP, resources.GetString("Tb_Server_IP.ToolTip"));
            //
            // Cb_Reconnect
            //
            resources.ApplyResources(this.Cb_Reconnect, "Cb_Reconnect");
            this.Cb_Reconnect.Name = "Cb_Reconnect";
            this.Pref_TlTip.SetToolTip(this.Cb_Reconnect, resources.GetString("Cb_Reconnect.ToolTip"));
            this.Cb_Reconnect.UseVisualStyleBackColor = true;
            //
            // Lbl_Delay_Com
            //
            resources.ApplyResources(this.Lbl_Delay_Com, "Lbl_Delay_Com");
            this.Lbl_Delay_Com.Name = "Lbl_Delay_Com";
            //
            // Lbl_Name_UPS
            //
            resources.ApplyResources(this.Lbl_Name_UPS, "Lbl_Name_UPS");
            this.Lbl_Name_UPS.Name = "Lbl_Name_UPS";
            //
            // Lbl_Port
            //
            resources.ApplyResources(this.Lbl_Port, "Lbl_Port");
            this.Lbl_Port.Name = "Lbl_Port";
            //
            // Lbl_Server_IP
            //
            resources.ApplyResources(this.Lbl_Server_IP, "Lbl_Server_IP");
            this.Lbl_Server_IP.Name = "Lbl_Server_IP";
            //
            // Tab_Calibrage
            //
            this.Tab_Calibrage.Controls.Add(this.Cbx_Freq_Input);
            this.Tab_Calibrage.Controls.Add(this.Tb_BattV_Max);
            this.Tab_Calibrage.Controls.Add(this.Tb_OutV_Max);
            this.Tab_Calibrage.Controls.Add(this.Tb_InF_Max);
            this.Tab_Calibrage.Controls.Add(this.Tb_BattV_Min);
            this.Tab_Calibrage.Controls.Add(this.Tb_OutV_Min);
            this.Tab_Calibrage.Controls.Add(this.Tb_InF_Min);
            this.Tab_Calibrage.Controls.Add(this.Tb_InV_Max);
            this.Tab_Calibrage.Controls.Add(this.Tb_InV_Min);
            this.Tab_Calibrage.Controls.Add(this.Lbl_Maxi);
            this.Tab_Calibrage.Controls.Add(this.Lbl_Mini);
            this.Tab_Calibrage.Controls.Add(this.Lbl_BattV);
            this.Tab_Calibrage.Controls.Add(this.Lbl_LoadUPS);
            this.Tab_Calibrage.Controls.Add(this.Lbl_OutputV);
            this.Tab_Calibrage.Controls.Add(this.Lbl_InputF);
            this.Tab_Calibrage.Controls.Add(this.Lbl_PowerF);
            this.Tab_Calibrage.Controls.Add(this.Lbl_InputV);
            resources.ApplyResources(this.Tab_Calibrage, "Tab_Calibrage");
            this.Tab_Calibrage.Name = "Tab_Calibrage";
            this.Tab_Calibrage.UseVisualStyleBackColor = true;
            //
            // Cbx_Freq_Input
            //
            this.Cbx_Freq_Input.FormattingEnabled = true;
            this.Cbx_Freq_Input.Items.AddRange(new object[] { resources.GetString("Cbx_Freq_Input.Items"), resources.GetString("Cbx_Freq_Input.Items1") });
            resources.ApplyResources(this.Cbx_Freq_Input, "Cbx_Freq_Input");
            this.Cbx_Freq_Input.Name = "Cbx_Freq_Input";
            this.Pref_TlTip.SetToolTip(this.Cbx_Freq_Input, resources.GetString("Cbx_Freq_Input.ToolTip"));
            //
            // Tb_BattV_Max
            //
            resources.ApplyResources(this.Tb_BattV_Max, "Tb_BattV_Max");
            this.Tb_BattV_Max.Name = "Tb_BattV_Max";
            this.Pref_TlTip.SetToolTip(this.Tb_BattV_Max, resources.GetString("Tb_BattV_Max.ToolTip"));
            //
            // Tb_OutV_Max
            //
            resources.ApplyResources(this.Tb_OutV_Max, "Tb_OutV_Max");
            this.Tb_OutV_Max.Name = "Tb_OutV_Max";
            this.Pref_TlTip.SetToolTip(this.Tb_OutV_Max, resources.GetString("Tb_OutV_Max.ToolTip"));
            //
            // Tb_InF_Max
            //
            resources.ApplyResources(this.Tb_InF_Max, "Tb_InF_Max");
            this.Tb_InF_Max.Name = "Tb_InF_Max";
            this.Pref_TlTip.SetToolTip(this.Tb_InF_Max, resources.GetString("Tb_InF_Max.ToolTip"));
            //
            // Tb_BattV_Min
            //
            resources.ApplyResources(this.Tb_BattV_Min, "Tb_BattV_Min");
            this.Tb_BattV_Min.Name = "Tb_BattV_Min";
            this.Pref_TlTip.SetToolTip(this.Tb_BattV_Min, resources.GetString("Tb_BattV_Min.ToolTip"));
            //
            // Tb_OutV_Min
            //
            resources.ApplyResources(this.Tb_OutV_Min, "Tb_OutV_Min");
            this.Tb_OutV_Min.Name = "Tb_OutV_Min";
            this.Pref_TlTip.SetToolTip(this.Tb_OutV_Min, resources.GetString("Tb_OutV_Min.ToolTip"));
            //
            // Tb_InF_Min
            //
            resources.ApplyResources(this.Tb_InF_Min, "Tb_InF_Min");
            this.Tb_InF_Min.Name = "Tb_InF_Min";
            this.Pref_TlTip.SetToolTip(this.Tb_InF_Min, resources.GetString("Tb_InF_Min.ToolTip"));
            //
            // Tb_InV_Max
            //
            resources.ApplyResources(this.Tb_InV_Max, "Tb_InV_Max");
            this.Tb_InV_Max.Name = "Tb_InV_Max";
            this.Pref_TlTip.SetToolTip(this.Tb_InV_Max, resources.GetString("Tb_InV_Max.ToolTip"));
            //
            // Tb_InV_Min
            //
            resources.ApplyResources(this.Tb_InV_Min, "Tb_InV_Min");
            this.Tb_InV_Min.Name = "Tb_InV_Min";
            this.Pref_TlTip.SetToolTip(this.Tb_InV_Min, resources.GetString("Tb_InV_Min.ToolTip"));
            //
            // Lbl_Maxi
            //
            resources.ApplyResources(this.Lbl_Maxi, "Lbl_Maxi");
            this.Lbl_Maxi.Name = "Lbl_Maxi";
            //
            // Lbl_Mini
            //
            resources.ApplyResources(this.Lbl_Mini, "Lbl_Mini");
            this.Lbl_Mini.Name = "Lbl_Mini";
            //
            // Lbl_BattV
            //
            resources.ApplyResources(this.Lbl_BattV, "Lbl_BattV");
            this.Lbl_BattV.Name = "Lbl_BattV";
            //
            // Lbl_LoadUPS
            //
            resources.ApplyResources(this.Lbl_LoadUPS, "Lbl_LoadUPS");
            this.Lbl_LoadUPS.ForeColor = System.Drawing.SystemColors.GrayText;
            this.Lbl_LoadUPS.Name = "Lbl_LoadUPS";
            //
            // Lbl_OutputV
            //
            resources.ApplyResources(this.Lbl_OutputV, "Lbl_OutputV");
            this.Lbl_OutputV.Name = "Lbl_OutputV";
            //
            // Lbl_InputF
            //
            resources.ApplyResources(this.Lbl_InputF, "Lbl_InputF");
            this.Lbl_InputF.Name = "Lbl_InputF";
            //
            // Lbl_PowerF
            //
            resources.ApplyResources(this.Lbl_PowerF, "Lbl_PowerF");
            this.Lbl_PowerF.Name = "Lbl_PowerF";
            //
            // Lbl_InputV
            //
            resources.ApplyResources(this.Lbl_InputV, "Lbl_InputV");
            this.Lbl_InputV.Name = "Lbl_InputV";
            //
            // Tab_Miscellanous
            //
            this.Tab_Miscellanous.Controls.Add(this.Cbx_LogLevel);
            this.Tab_Miscellanous.Controls.Add(this.Btn_DeleteLog);
            this.Tab_Miscellanous.Controls.Add(this.Btn_ViewLog);
            this.Tab_Miscellanous.Controls.Add(this.Lbl_LevelLog);
            this.Tab_Miscellanous.Controls.Add(this.CB_Use_Logfile);
            this.Tab_Miscellanous.Controls.Add(this.CB_Start_W_Win);
            this.Tab_Miscellanous.Controls.Add(this.CB_Close_Tray);
            this.Tab_Miscellanous.Controls.Add(this.CB_Start_Mini);
            this.Tab_Miscellanous.Controls.Add(this.CB_Systray);
            resources.ApplyResources(this.Tab_Miscellanous, "Tab_Miscellanous");
            this.Tab_Miscellanous.Name = "Tab_Miscellanous";
            this.Tab_Miscellanous.UseVisualStyleBackColor = true;
            //
            // Cbx_LogLevel
            //
            this.Cbx_LogLevel.FormattingEnabled = true;
            this.Cbx_LogLevel.Items.AddRange(new object[] { resources.GetString("Cbx_LogLevel.Items"), resources.GetString("Cbx_LogLevel.Items1"), resources.GetString("Cbx_LogLevel.Items2"), resources.GetString("Cbx_LogLevel.Items3") });
            resources.ApplyResources(this.Cbx_LogLevel, "Cbx_LogLevel");
            this.Cbx_LogLevel.Name = "Cbx_LogLevel";
            this.Pref_TlTip.SetToolTip(this.Cbx_LogLevel, resources.GetString("Cbx_LogLevel.ToolTip"));
            //
            // Btn_DeleteLog
            //
            this.Btn_DeleteLog.Image = (System.Drawing.Image)global::NutDesk.Properties.Resources.Delete_LogFile_24x24;
            resources.ApplyResources(this.Btn_DeleteLog, "Btn_DeleteLog");
            this.Btn_DeleteLog.Name = "Btn_DeleteLog";
            this.Pref_TlTip.SetToolTip(this.Btn_DeleteLog, resources.GetString("Btn_DeleteLog.ToolTip"));
            this.Btn_DeleteLog.UseVisualStyleBackColor = true;
            //
            // Btn_ViewLog
            //
            this.Btn_ViewLog.Image = (System.Drawing.Image)global::NutDesk.Properties.Resources.ViewLogFile_24x24;
            resources.ApplyResources(this.Btn_ViewLog, "Btn_ViewLog");
            this.Btn_ViewLog.Name = "Btn_ViewLog";
            this.Pref_TlTip.SetToolTip(this.Btn_ViewLog, resources.GetString("Btn_ViewLog.ToolTip"));
            this.Btn_ViewLog.UseVisualStyleBackColor = true;
            //
            // Lbl_LevelLog
            //
            resources.ApplyResources(this.Lbl_LevelLog, "Lbl_LevelLog");
            this.Lbl_LevelLog.Name = "Lbl_LevelLog";
            this.Pref_TlTip.SetToolTip(this.Lbl_LevelLog, resources.GetString("Lbl_LevelLog.ToolTip"));
            //
            // CB_Use_Logfile
            //
            resources.ApplyResources(this.CB_Use_Logfile, "CB_Use_Logfile");
            this.CB_Use_Logfile.Name = "CB_Use_Logfile";
            this.Pref_TlTip.SetToolTip(this.CB_Use_Logfile, resources.GetString("CB_Use_Logfile.ToolTip"));
            this.CB_Use_Logfile.UseVisualStyleBackColor = true;
            //
            // CB_Start_W_Win
            //
            resources.ApplyResources(this.CB_Start_W_Win, "CB_Start_W_Win");
            this.CB_Start_W_Win.Name = "CB_Start_W_Win";
            this.Pref_TlTip.SetToolTip(this.CB_Start_W_Win, resources.GetString("CB_Start_W_Win.ToolTip"));
            this.CB_Start_W_Win.UseVisualStyleBackColor = true;
            //
            // CB_Close_Tray
            //
            resources.ApplyResources(this.CB_Close_Tray, "CB_Close_Tray");
            this.CB_Close_Tray.Name = "CB_Close_Tray";
            this.Pref_TlTip.SetToolTip(this.CB_Close_Tray, resources.GetString("CB_Close_Tray.ToolTip"));
            this.CB_Close_Tray.UseVisualStyleBackColor = true;
            //
            // CB_Start_Mini
            //
            resources.ApplyResources(this.CB_Start_Mini, "CB_Start_Mini");
            this.CB_Start_Mini.Name = "CB_Start_Mini";
            this.Pref_TlTip.SetToolTip(this.CB_Start_Mini, resources.GetString("CB_Start_Mini.ToolTip"));
            this.CB_Start_Mini.UseVisualStyleBackColor = true;
            //
            // CB_Systray
            //
            resources.ApplyResources(this.CB_Systray, "CB_Systray");
            this.CB_Systray.Name = "CB_Systray";
            this.Pref_TlTip.SetToolTip(this.CB_Systray, resources.GetString("CB_Systray.ToolTip"));
            this.CB_Systray.UseVisualStyleBackColor = true;
            //
            // Tab_Shutdown
            //
            this.Tab_Shutdown.Controls.Add(this.CB_Follow_FSD);
            this.Tab_Shutdown.Controls.Add(this.Lbl_Percent);
            this.Tab_Shutdown.Controls.Add(this.Tb_GraceTime);
            this.Tab_Shutdown.Controls.Add(this.Cb_ExtendTime);
            this.Tab_Shutdown.Controls.Add(this.Tb_Delay_Stop);
            this.Tab_Shutdown.Controls.Add(this.Cbx_TypeStop);
            this.Tab_Shutdown.Controls.Add(this.Cb_ImmediateStop);
            this.Tab_Shutdown.Controls.Add(this.Tb_BattLimit_Time);
            this.Tab_Shutdown.Controls.Add(this.Tb_BattLimit_Load);
            this.Tab_Shutdown.Controls.Add(this.Lbl_GraceTime);
            this.Tab_Shutdown.Controls.Add(this.Lbl_Delay_Stop);
            this.Tab_Shutdown.Controls.Add(this.Lbl_StopType);
            this.Tab_Shutdown.Controls.Add(this.Lbl_BattLimit_Time);
            this.Tab_Shutdown.Controls.Add(this.Lbl_BattLimit_Load);
            resources.ApplyResources(this.Tab_Shutdown, "Tab_Shutdown");
            this.Tab_Shutdown.Name = "Tab_Shutdown";
            this.Tab_Shutdown.UseVisualStyleBackColor = true;
            //
            // CB_Follow_FSD
            //
            resources.ApplyResources(this.CB_Follow_FSD, "CB_Follow_FSD");
            this.CB_Follow_FSD.Name = "CB_Follow_FSD";
            this.Pref_TlTip.SetToolTip(this.CB_Follow_FSD, resources.GetString("CB_Follow_FSD.ToolTip"));
            this.CB_Follow_FSD.UseVisualStyleBackColor = true;
            //
            // Lbl_Percent
            //
            resources.ApplyResources(this.Lbl_Percent, "Lbl_Percent");
            this.Lbl_Percent.Name = "Lbl_Percent";
            //
            // Tb_GraceTime
            //
            resources.ApplyResources(this.Tb_GraceTime, "Tb_GraceTime");
            this.Tb_GraceTime.Name = "Tb_GraceTime";
            this.Pref_TlTip.SetToolTip(this.Tb_GraceTime, resources.GetString("Tb_GraceTime.ToolTip"));
            //
            // Cb_ExtendTime
            //
            resources.ApplyResources(this.Cb_ExtendTime, "Cb_ExtendTime");
            this.Cb_ExtendTime.Name = "Cb_ExtendTime";
            this.Pref_TlTip.SetToolTip(this.Cb_ExtendTime, resources.GetString("Cb_ExtendTime.ToolTip"));
            this.Cb_ExtendTime.UseVisualStyleBackColor = true;
            //
            // Tb_Delay_Stop
            //
            resources.ApplyResources(this.Tb_Delay_Stop, "Tb_Delay_Stop");
            this.Tb_Delay_Stop.Name = "Tb_Delay_Stop";
            this.Pref_TlTip.SetToolTip(this.Tb_Delay_Stop, resources.GetString("Tb_Delay_Stop.ToolTip"));
            //
            // Cbx_TypeStop
            //
            this.Cbx_TypeStop.FormattingEnabled = true;
            this.Cbx_TypeStop.Items.AddRange(new object[] { resources.GetString("Cbx_TypeStop.Items"), resources.GetString("Cbx_TypeStop.Items1"), resources.GetString("Cbx_TypeStop.Items2") });
            resources.ApplyResources(this.Cbx_TypeStop, "Cbx_TypeStop");
            this.Cbx_TypeStop.Name = "Cbx_TypeStop";
            this.Pref_TlTip.SetToolTip(this.Cbx_TypeStop, resources.GetString("Cbx_TypeStop.ToolTip"));
            //
            // Cb_ImmediateStop
            //
            resources.ApplyResources(this.Cb_ImmediateStop, "Cb_ImmediateStop");
            this.Cb_ImmediateStop.Name = "Cb_ImmediateStop";
            this.Pref_TlTip.SetToolTip(this.Cb_ImmediateStop, resources.GetString("Cb_ImmediateStop.ToolTip"));
            this.Cb_ImmediateStop.UseVisualStyleBackColor = true;
            //
            // Tb_BattLimit_Time
            //
            resources.ApplyResources(this.Tb_BattLimit_Time, "Tb_BattLimit_Time");
            this.Tb_BattLimit_Time.Name = "Tb_BattLimit_Time";
            this.Pref_TlTip.SetToolTip(this.Tb_BattLimit_Time, resources.GetString("Tb_BattLimit_Time.ToolTip"));
            //
            // Tb_BattLimit_Load
            //
            resources.ApplyResources(this.Tb_BattLimit_Load, "Tb_BattLimit_Load");
            this.Tb_BattLimit_Load.Name = "Tb_BattLimit_Load";
            this.Pref_TlTip.SetToolTip(this.Tb_BattLimit_Load, resources.GetString("Tb_BattLimit_Load.ToolTip"));
            //
            // Lbl_GraceTime
            //
            resources.ApplyResources(this.Lbl_GraceTime, "Lbl_GraceTime");
            this.Lbl_GraceTime.Name = "Lbl_GraceTime";
            //
            // Lbl_Delay_Stop
            //
            resources.ApplyResources(this.Lbl_Delay_Stop, "Lbl_Delay_Stop");
            this.Lbl_Delay_Stop.Name = "Lbl_Delay_Stop";
            //
            // Lbl_StopType
            //
            resources.ApplyResources(this.Lbl_StopType, "Lbl_StopType");
            this.Lbl_StopType.Name = "Lbl_StopType";
            //
            // Lbl_BattLimit_Time
            //
            resources.ApplyResources(this.Lbl_BattLimit_Time, "Lbl_BattLimit_Time");
            this.Lbl_BattLimit_Time.Name = "Lbl_BattLimit_Time";
            //
            // Lbl_BattLimit_Load
            //
            resources.ApplyResources(this.Lbl_BattLimit_Load, "Lbl_BattLimit_Load");
            this.Lbl_BattLimit_Load.Name = "Lbl_BattLimit_Load";
            //
            // Tab_Update
            //
            this.Tab_Update.Controls.Add(this.Cbx_Branch_Update);
            this.Tab_Update.Controls.Add(this.Cbx_Delay_Verif);
            this.Tab_Update.Controls.Add(this.Lbl_Branch_Update);
            this.Tab_Update.Controls.Add(this.Lbl_Delay_Verif);
            this.Tab_Update.Controls.Add(this.Cb_Update_At_Start);
            resources.ApplyResources(this.Tab_Update, "Tab_Update");
            this.Tab_Update.Name = "Tab_Update";
            this.Tab_Update.UseVisualStyleBackColor = true;
            //
            // Cbx_Branch_Update
            //
            this.Cbx_Branch_Update.FormattingEnabled = true;
            this.Cbx_Branch_Update.Items.AddRange(new object[] { resources.GetString("Cbx_Branch_Update.Items"), resources.GetString("Cbx_Branch_Update.Items1") });
            resources.ApplyResources(this.Cbx_Branch_Update, "Cbx_Branch_Update");
            this.Cbx_Branch_Update.Name = "Cbx_Branch_Update";
            this.Pref_TlTip.SetToolTip(this.Cbx_Branch_Update, resources.GetString("Cbx_Branch_Update.ToolTip"));
            //
            // Cbx_Delay_Verif
            //
            this.Cbx_Delay_Verif.FormattingEnabled = true;
            this.Cbx_Delay_Verif.Items.AddRange(new object[] { resources.GetString("Cbx_Delay_Verif.Items"), resources.GetString("Cbx_Delay_Verif.Items1"), resources.GetString("Cbx_Delay_Verif.Items2") });
            resources.ApplyResources(this.Cbx_Delay_Verif, "Cbx_Delay_Verif");
            this.Cbx_Delay_Verif.Name = "Cbx_Delay_Verif";
            this.Pref_TlTip.SetToolTip(this.Cbx_Delay_Verif, resources.GetString("Cbx_Delay_Verif.ToolTip"));
            //
            // Lbl_Branch_Update
            //
            resources.ApplyResources(this.Lbl_Branch_Update, "Lbl_Branch_Update");
            this.Lbl_Branch_Update.Name = "Lbl_Branch_Update";
            //
            // Lbl_Delay_Verif
            //
            resources.ApplyResources(this.Lbl_Delay_Verif, "Lbl_Delay_Verif");
            this.Lbl_Delay_Verif.Name = "Lbl_Delay_Verif";
            //
            // Cb_Update_At_Start
            //
            resources.ApplyResources(this.Cb_Update_At_Start, "Cb_Update_At_Start");
            this.Cb_Update_At_Start.Name = "Cb_Update_At_Start";
            this.Pref_TlTip.SetToolTip(this.Cb_Update_At_Start, resources.GetString("Cb_Update_At_Start.ToolTip"));
            this.Cb_Update_At_Start.UseVisualStyleBackColor = true;
            //
            // Btn_Ok
            //
            resources.ApplyResources(this.Btn_Ok, "Btn_Ok");
            this.Btn_Ok.Name = "Btn_Ok";
            this.Btn_Ok.UseVisualStyleBackColor = true;
            //
            // Btn_Apply
            //
            resources.ApplyResources(this.Btn_Apply, "Btn_Apply");
            this.Btn_Apply.Name = "Btn_Apply";
            this.Btn_Apply.UseVisualStyleBackColor = true;
            //
            // Btn_Cancel
            //
            resources.ApplyResources(this.Btn_Cancel, "Btn_Cancel");
            this.Btn_Cancel.Name = "Btn_Cancel";
            //
            // Pref_Gui
            //
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.Controls.Add(this.Btn_Cancel);
            this.Controls.Add(this.Btn_Apply);
            this.Controls.Add(this.Btn_Ok);
            this.Controls.Add(this.TabControl_Options);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "Pref_Gui";
            this.ShowIcon = false;
            this.TabControl_Options.ResumeLayout(false);
            this.Tab_Connexion.ResumeLayout(false);
            this.Tab_Connexion.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)this.pollingIntervalValue).EndInit();
            this.Tab_Calibrage.ResumeLayout(false);
            this.Tab_Calibrage.PerformLayout();
            this.Tab_Miscellanous.ResumeLayout(false);
            this.Tab_Miscellanous.PerformLayout();
            this.Tab_Shutdown.ResumeLayout(false);
            this.Tab_Shutdown.PerformLayout();
            this.Tab_Update.ResumeLayout(false);
            this.Tab_Update.PerformLayout();
            this.ResumeLayout(false);
        }

        internal System.Windows.Forms.TabControl TabControl_Options;
        internal System.Windows.Forms.TabPage Tab_Connexion;
        internal System.Windows.Forms.CheckBox Cb_Reconnect;
        internal System.Windows.Forms.Label Lbl_Delay_Com;
        internal System.Windows.Forms.Label Lbl_Name_UPS;
        internal System.Windows.Forms.Label Lbl_Port;
        internal System.Windows.Forms.Label Lbl_Server_IP;
        internal System.Windows.Forms.TabPage Tab_Calibrage;
        internal System.Windows.Forms.TabPage Tab_Miscellanous;
        internal System.Windows.Forms.TabPage Tab_Shutdown;
        internal System.Windows.Forms.TabPage Tab_Update;
        internal System.Windows.Forms.Button Btn_Ok;
        internal System.Windows.Forms.Button Btn_Apply;
        internal System.Windows.Forms.TextBox Tb_Server_IP;
        internal System.Windows.Forms.TextBox Tb_Port;
        internal System.Windows.Forms.TextBox Tb_UPS_Name;
        internal System.Windows.Forms.Label Lbl_InputV;
        internal System.Windows.Forms.Label Lbl_BattV;
        internal System.Windows.Forms.Label Lbl_LoadUPS;
        internal System.Windows.Forms.Label Lbl_OutputV;
        internal System.Windows.Forms.Label Lbl_InputF;
        internal System.Windows.Forms.Label Lbl_PowerF;
        internal System.Windows.Forms.TextBox Tb_InV_Min;
        internal System.Windows.Forms.Label Lbl_Maxi;
        internal System.Windows.Forms.Label Lbl_Mini;
        internal System.Windows.Forms.ComboBox Cbx_Freq_Input;
        internal System.Windows.Forms.TextBox Tb_BattV_Max;
        internal System.Windows.Forms.TextBox Tb_OutV_Max;
        internal System.Windows.Forms.TextBox Tb_InF_Max;
        internal System.Windows.Forms.TextBox Tb_BattV_Min;
        internal System.Windows.Forms.TextBox Tb_OutV_Min;
        internal System.Windows.Forms.TextBox Tb_InF_Min;
        internal System.Windows.Forms.TextBox Tb_InV_Max;
        internal System.Windows.Forms.Button Btn_DeleteLog;
        internal System.Windows.Forms.Button Btn_ViewLog;
        internal System.Windows.Forms.Label Lbl_LevelLog;
        internal System.Windows.Forms.CheckBox CB_Use_Logfile;
        internal System.Windows.Forms.CheckBox CB_Start_W_Win;
        internal System.Windows.Forms.CheckBox CB_Close_Tray;
        internal System.Windows.Forms.CheckBox CB_Start_Mini;
        internal System.Windows.Forms.CheckBox CB_Systray;
        internal System.Windows.Forms.ComboBox Cbx_LogLevel;
        internal System.Windows.Forms.Label Lbl_Delay_Stop;
        internal System.Windows.Forms.Label Lbl_StopType;
        internal System.Windows.Forms.Label Lbl_BattLimit_Time;
        internal System.Windows.Forms.Label Lbl_BattLimit_Load;
        internal System.Windows.Forms.Label Lbl_Percent;
        internal System.Windows.Forms.TextBox Tb_GraceTime;
        internal System.Windows.Forms.CheckBox Cb_ExtendTime;
        internal System.Windows.Forms.TextBox Tb_Delay_Stop;
        internal System.Windows.Forms.ComboBox Cbx_TypeStop;
        internal System.Windows.Forms.CheckBox Cb_ImmediateStop;
        internal System.Windows.Forms.TextBox Tb_BattLimit_Time;
        internal System.Windows.Forms.TextBox Tb_BattLimit_Load;
        internal System.Windows.Forms.Label Lbl_GraceTime;
        internal System.Windows.Forms.ToolTip Pref_TlTip;
        internal System.Windows.Forms.ComboBox Cbx_Branch_Update;
        internal System.Windows.Forms.ComboBox Cbx_Delay_Verif;
        internal System.Windows.Forms.Label Lbl_Branch_Update;
        internal System.Windows.Forms.Label Lbl_Delay_Verif;
        internal System.Windows.Forms.CheckBox Cb_Update_At_Start;
        internal System.Windows.Forms.TextBox Tb_Pwd_Nut;
        internal System.Windows.Forms.TextBox Tb_Login_Nut;
        internal System.Windows.Forms.Label Label3;
        internal System.Windows.Forms.Label Lbl_Pwd_Nut;
        internal System.Windows.Forms.Label Lbl_Login_Nut;
        internal System.Windows.Forms.CheckBox CB_Follow_FSD;
        internal System.Windows.Forms.Label pollingIntervalUnitLabel;
        private System.Windows.Forms.NumericUpDown pollingIntervalValue;
        private System.Windows.Forms.Button Btn_Cancel;
    }
}
