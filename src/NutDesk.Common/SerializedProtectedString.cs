// WinNUT-Client is a NUT windows client for monitoring your ups hooked up to your favorite linux server.
// Copyright (C) 2019-2021 Gawindx (Decaux Nicolas)
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or any later version.
//
// This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY

using System;
using System.Security.Cryptography;
using System.Text;

namespace NutDesk.Common
{
    public class SerializedProtectedString
    {
        private string protectedString;

        // Make the encrypted string available for serialization.
        public string ProtectedValue
        {
            get => protectedString;
            set => protectedString = value;
        }

        // Do not save the unprotected string when serializing.
        private string UnprotectedValue
        {
            get => Unprotect(protectedString);
            set => protectedString = Convert.ToBase64String(ProtectedData.Protect(Encoding.Unicode.GetBytes(value),
                                                        null, DataProtectionScope.CurrentUser));
        }

        /// <summary>
        /// Create a new object starting with unencrypted data by default.
        /// </summary>
        /// <param name="data">Data that needs to be encrypted.</param>
        /// <param name="alreadyEncrypted">The provided data is already encrypted and the first encryption should
        /// be skipped. Default to false (data is starting as unencrypted.)</param>
        public SerializedProtectedString(string data, bool alreadyEncrypted = false)
        {
            if (alreadyEncrypted)
            {
                protectedString = data;
            }
            else
            {
                UnprotectedValue = data;
            }
        }

        /// <summary>
        /// Provide default constructor to create an empty object.
        /// </summary>
        public SerializedProtectedString() : this(string.Empty)
        {
        }

        /// <summary>
        /// Attempts to unprotect an arbitrary string, suppressing any exceptions in the process.
        /// Note: Use this function sparingly. Call the parametered constructor
        /// <see cref="SerializedProtectedString(string, bool)"/> when the protection state is already known.
        /// </summary>
        /// <param name="unknownString"></param>
        /// <returns>Either the unprotected value of the given parameter, or the parameter's value its self.</returns>
        private static string TryUnprotect(string unknownString)
        {
            try
            {
                return Unprotect(unknownString);
            }
            catch (Exception)
            {
                return unknownString;
            }
        }

        private static string Unprotect(string protectedString)
        {
            return Encoding.Unicode.GetString(ProtectedData.Unprotect(Convert.FromBase64String(protectedString),
                                       null, DataProtectionScope.CurrentUser));
        }

        /// <summary>
        /// Provide the decrypted string.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return UnprotectedValue;
        }

        /// <summary>
        /// Cast an arbitrary (unknown state) String into this object.
        /// </summary>
        /// <param name="unkStr"></param>
        /// <returns></returns>
        public static explicit operator SerializedProtectedString(string unkStr)
        {
            return new SerializedProtectedString(TryUnprotect(unkStr));
        }

        /// <summary>
        /// Support casting to a String, by providing the unprotected value (default ToString operation.)
        /// </summary>
        /// <param name="protStr"></param>
        /// <returns></returns>
        public static implicit operator string(SerializedProtectedString protStr)
        {
            if (protStr != null)
            {
                return protStr.ToString();
            }
            else
            {
                return null;
            }
        }
    }
}
