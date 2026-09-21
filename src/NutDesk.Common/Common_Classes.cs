using System;

namespace NutDesk.Common
{
    public class UPS_Var_Node
    {
        public string VarKey;
        public string VarValue;
        public string VarDesc;
    }

    public class UPS_List_Datas
    {
        public string VarKey;
        public string VarValue;
        public string VarDesc;
    }

    public class UPS_Values
    {
        // Every field below was written as "= Nothing" in VB, which on a value type simply means the
        // type's default value (0 for the numerics, None for the flags enum.)
        public double Batt_Charge = 0;
        public double Batt_Voltage = 0;
        public double Batt_Runtime = 0;
        public double Input_Voltage = 0;
        public double Output_Voltage = 0;
        public float? Output_Current;
        public double Power_Frequency = 0;
        public double Load = 0;
        public double Output_Power = 0;
        public double Batt_Capacity = 0;
        public UPS_States UPS_Status = UPS_States.None;
    }

    public class UPSData
    {
        public readonly string Mfr;
        public readonly string Model;
        public readonly string Serial;
        public readonly string Firmware;
        public UPS_Values UPS_Value = new UPS_Values();

        public UPSData(string Mfr, string Model, string Serial, string Firmware)
        {
            this.Mfr = Mfr;
            this.Model = Model;
            this.Serial = Serial;
            this.Firmware = Firmware;
        }
    }

    /// <summary>
    /// Encapsulates a query and response between a NUT client and server.
    /// </summary>
    public class Transaction
    {
        /// <summary>
        /// The original query sent by the client, to the server.
        /// </summary>
        /// <returns></returns>
        public string Query { get; }

        /// <summary>
        /// See <see cref="NUTResponse"/>
        /// </summary>
        /// <returns></returns>
        public NUTResponse ResponseType { get; }

        /// <summary>
        /// The full, unaltered response from the server.
        /// </summary>
        /// <returns></returns>
        public string RawResponse { get; }

        /// <summary>
        /// A <see cref="RawResponse"/> that has been split around the delimeter character (space)
        /// </summary>
        public string[] SplitResponse { get; }

        public Transaction(string query, string response, NUTResponse responseType, string[] splitResponse = null)
        {
            Query = query;
            RawResponse = response;
            ResponseType = responseType;
            SplitResponse = splitResponse;
        }
    }

    public class NutException : ApplicationException
    {
        public Transaction LastTransaction { get; }

        /// <summary>
        /// Raise an exception that resulted from a defined error in the NUT protocol.
        /// </summary>
        public NutException(Transaction transaction)
            : base(string.Format("{0} ({1})" + Environment.NewLine + "Query: {2}", transaction.ResponseType,
                                 transaction.RawResponse, transaction.Query))
        {
            LastTransaction = transaction;
        }
    }

    public class Nut_Parameter
    {
        public string Host = "";
        public int Port = 0;
        public string Login = "";
        public string Password = "";
        public string UPSName = "";
        public bool AutoReconnect = false;

        public Nut_Parameter(string Host, int Port, string Login, string Password, string UPSName,
                             bool AutoReconnect = false)
        {
            this.Host = Host;
            this.Port = Port;
            this.Login = Login;
            this.Password = Password;
            this.UPSName = UPSName;
            this.AutoReconnect = AutoReconnect;
        }

        /// <summary>
        /// Generate an informative String representing this Parameter object. Note password is not printed.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            // The " [AutoReconnect]" suffix is appended to the format string itself, exactly as the VB code did.
            // A sixth argument (AutoReconnect) is passed but never referenced by a placeholder; kept as-is.
            return string.Format("{0}@{1}:{2}, Name: {3}" + (AutoReconnect ? " [AutoReconnect]" : null),
                                 Login, Host, Port, UPSName, AutoReconnect);
            // Return MyBase.ToString())
        }
    }
}
