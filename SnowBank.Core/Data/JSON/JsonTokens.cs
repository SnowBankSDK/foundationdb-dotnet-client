#region Copyright (c) 2023-2026 SnowBank SAS, (c) 2005-2023 Doxense SAS
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 	* Redistributions of source code must retain the above copyright
// 	  notice, this list of conditions and the following disclaimer.
// 	* Redistributions in binary form must reproduce the above copyright
// 	  notice, this list of conditions and the following disclaimer in the
// 	  documentation and/or other materials provided with the distribution.
// 	* Neither the name of SnowBank nor the
// 	  names of its contributors may be used to endorse or promote products
// 	  derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL SNOWBANK SAS BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion

namespace SnowBank.Data.Json
{

	/// <summary>Internal class that holds various constants and tokens used during parsing and serialization</summary>
	internal static class JsonTokens
	{

		public const string Null = "null";
		public const string Undefined = "undefined";
		public const string True = "true";
		public const string False = "false";
		public const string SymbolNaN = "NaN";
		public const string SymbolInfinityPos = "Infinity";
		public const string SymbolInfinityNeg = "-Infinity";
		public const string StringNaN = "\"NaN\"";
		public const string StringInfinityPos = "\"Infinity\"";
		public const string StringInfinityNeg = "\"-Infinity\"";
		public const string JavaScriptNaN = "Number.NaN";
		public const string JavaScriptInfinityPos = "Number.POSITIVE_INFINITY";
		public const string JavaScriptInfinityNeg = "Number.NEGATIVE_INFINITY";
		public const string LongMinValue = "-9223372036854775808";
		public const string DateBeginMicrosoft = "\"\\/Date(";
		public const string DateEndMicrosoft = ")\\/\"";
		public const string DateBeginJavaScript = "new Date(";
		public const string MicrosoftDateTimeMinValue = "\"\\/Date(-62135596800000)\\/\"";
		public const string MicrosoftDateTimeMaxValue = "\"\\/Date(253402300799999)\\/\"";
		public const string JavaScriptDateTimeMinValue = "new Date(-62135596800000)";
		public const string JavaScriptDateTimeMaxValue = "new Date(253402300799999)";
		public const string Iso8601DateTimeMaxValue = "\"9999-12-31T23:59:59.9999999\"";
		public const string Iso8601DateOnlyMaxValue = "\"9999-12-31\"";
		public const string CustomClassAttribute = "$type";

	}

}
