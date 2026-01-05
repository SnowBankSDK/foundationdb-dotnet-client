#region Copyright (c) 2023-2025 SnowBank SAS, (c) 2005-2023 Doxense SAS
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

namespace SnowBank.Data.Json.Tests
{

	public partial class CrystalJsonTest
	{

		[Test]
		public void Test_JsonSettings_DefaultValues()
		{
			// By default, should have mostly default values for the properties (0 / false)
			using (Assert.EnterMultipleScope())
			{
				var settings = CrystalJsonSettings.Json;
				Assert.That(settings.TargetLanguage, Is.EqualTo(CrystalJsonSettings.Target.Json));
				Assert.That(settings.IsJsonTarget(), Is.True);
				Assert.That(settings.IsJavascriptTarget(), Is.False);
				Assert.That(settings.TextLayout, Is.EqualTo(CrystalJsonSettings.Layout.Formatted));
				Assert.That(settings.IsCompactLayout(), Is.False);
				Assert.That(settings.IsFormattedLayout(), Is.True);
				Assert.That(settings.IsIndentedLayout(), Is.False);
				Assert.That(settings.DateFormatting, Is.EqualTo(CrystalJsonSettings.DateFormat.Default));
				Assert.That(settings.InterningMode, Is.EqualTo(CrystalJsonSettings.StringInterning.Default));
				Assert.That(settings.HideDefaultValues, Is.False);
				Assert.That(settings.ShowNullMembers, Is.False);
				Assert.That(settings.UseCamelCasingForNames, Is.False);
				Assert.That(settings.OptimizeForLargeData, Is.False);
				Assert.That(settings.ReadOnly, Is.False);
				Assert.That(settings.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.None));
				Assert.That(CrystalJsonSettings.Json, Is.SameAs(settings));
			}

			// JsonImmutable
			using (Assert.EnterMultipleScope())
			{
				var settings = CrystalJsonSettings.JsonReadOnly;
				Assert.That(settings.TargetLanguage, Is.EqualTo(CrystalJsonSettings.Target.Json));
				Assert.That(settings.IsJsonTarget(), Is.True);
				Assert.That(settings.IsJavascriptTarget(), Is.False);
				Assert.That(settings.TextLayout, Is.EqualTo(CrystalJsonSettings.Layout.Formatted));
				Assert.That(settings.IsCompactLayout(), Is.False);
				Assert.That(settings.IsFormattedLayout(), Is.True);
				Assert.That(settings.IsIndentedLayout(), Is.False);
				Assert.That(settings.DateFormatting, Is.EqualTo(CrystalJsonSettings.DateFormat.Default));
				Assert.That(settings.InterningMode, Is.EqualTo(CrystalJsonSettings.StringInterning.Default));
				Assert.That(settings.HideDefaultValues, Is.False);
				Assert.That(settings.ShowNullMembers, Is.False);
				Assert.That(settings.UseCamelCasingForNames, Is.False);
				Assert.That(settings.OptimizeForLargeData, Is.False);
				Assert.That(settings.ReadOnly, Is.True);
				Assert.That(settings.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.Mutability_ReadOnly));
				Assert.That(CrystalJsonSettings.JsonReadOnly, Is.SameAs(settings));
			}

			// JsonIndented: Only the TextLayout must be different
			using (Assert.EnterMultipleScope())
			{
				var settings = CrystalJsonSettings.JsonIndented;
				Assert.That(settings.TargetLanguage, Is.EqualTo(CrystalJsonSettings.Target.Json));
				Assert.That(settings.IsJsonTarget(), Is.True);
				Assert.That(settings.IsJavascriptTarget(), Is.False);
				Assert.That(settings.TextLayout, Is.EqualTo(CrystalJsonSettings.Layout.Indented));
				Assert.That(settings.IsCompactLayout(), Is.False);
				Assert.That(settings.IsFormattedLayout(), Is.False);
				Assert.That(settings.IsIndentedLayout(), Is.True);
				Assert.That(settings.DateFormatting, Is.EqualTo(CrystalJsonSettings.DateFormat.Default));
				Assert.That(settings.InterningMode, Is.EqualTo(CrystalJsonSettings.StringInterning.Default));
				Assert.That(settings.HideDefaultValues, Is.False);
				Assert.That(settings.ShowNullMembers, Is.False);
				Assert.That(settings.UseCamelCasingForNames, Is.False);
				Assert.That(settings.OptimizeForLargeData, Is.False);
				Assert.That(settings.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.Layout_Indented));
				Assert.That(CrystalJsonSettings.JsonIndented, Is.SameAs(settings));
			}

			// JsonCompact: Only the TextLayout must be different
			using (Assert.EnterMultipleScope())
			{
				var settings = CrystalJsonSettings.JsonCompact;
				Assert.That(settings.TargetLanguage, Is.EqualTo(CrystalJsonSettings.Target.Json));
				Assert.That(settings.IsJsonTarget(), Is.True);
				Assert.That(settings.IsJavascriptTarget(), Is.False);
				Assert.That(settings.TextLayout, Is.EqualTo(CrystalJsonSettings.Layout.Compact));
				Assert.That(settings.IsCompactLayout(), Is.True);
				Assert.That(settings.IsFormattedLayout(), Is.False);
				Assert.That(settings.IsIndentedLayout(), Is.False);
				Assert.That(settings.DateFormatting, Is.EqualTo(CrystalJsonSettings.DateFormat.Default));
				Assert.That(settings.InterningMode, Is.EqualTo(CrystalJsonSettings.StringInterning.Default));
				Assert.That(settings.HideDefaultValues, Is.False);
				Assert.That(settings.ShowNullMembers, Is.False);
				Assert.That(settings.UseCamelCasingForNames, Is.False);
				Assert.That(settings.OptimizeForLargeData, Is.False);
				Assert.That(settings.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.Layout_Compact));
				Assert.That(CrystalJsonSettings.JsonCompact, Is.SameAs(settings));
			}

			// JavaScript: should target the JavaScript language (single quotes, dates with the form "new Date(...)")
			using (Assert.EnterMultipleScope())
			{
				var settings = CrystalJsonSettings.JavaScript;
				Assert.That(settings.TargetLanguage, Is.EqualTo(CrystalJsonSettings.Target.JavaScript));
				Assert.That(settings.IsJsonTarget(), Is.False);
				Assert.That(settings.IsJavascriptTarget(), Is.True);
				Assert.That(settings.TextLayout, Is.EqualTo(CrystalJsonSettings.Layout.Formatted));
				Assert.That(settings.IsCompactLayout(), Is.False);
				Assert.That(settings.IsFormattedLayout(), Is.True);
				Assert.That(settings.IsIndentedLayout(), Is.False);
				Assert.That(settings.DateFormatting, Is.EqualTo(CrystalJsonSettings.DateFormat.Default));
				Assert.That(settings.InterningMode, Is.EqualTo(CrystalJsonSettings.StringInterning.Default));
				Assert.That(settings.HideDefaultValues, Is.False);
				Assert.That(settings.ShowNullMembers, Is.False);
				Assert.That(settings.UseCamelCasingForNames, Is.False);
				Assert.That(settings.OptimizeForLargeData, Is.False);
				Assert.That(settings.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.Target_JavaScript));
				Assert.That(CrystalJsonSettings.JavaScript, Is.SameAs(settings));
			}

			// JavaScriptIndented: same as JavaScript, but with a different TextLayout
			using (Assert.EnterMultipleScope())
			{
				var settings = CrystalJsonSettings.JavaScriptIndented;
				Assert.That(settings.TargetLanguage, Is.EqualTo(CrystalJsonSettings.Target.JavaScript));
				Assert.That(settings.IsJsonTarget(), Is.False);
				Assert.That(settings.IsJavascriptTarget(), Is.True);
				Assert.That(settings.TextLayout, Is.EqualTo(CrystalJsonSettings.Layout.Indented));
				Assert.That(settings.IsCompactLayout(), Is.False);
				Assert.That(settings.IsFormattedLayout(), Is.False);
				Assert.That(settings.IsIndentedLayout(), Is.True);
				Assert.That(settings.DateFormatting, Is.EqualTo(CrystalJsonSettings.DateFormat.Default));
				Assert.That(settings.InterningMode, Is.EqualTo(CrystalJsonSettings.StringInterning.Default));
				Assert.That(settings.HideDefaultValues, Is.False);
				Assert.That(settings.ShowNullMembers, Is.False);
				Assert.That(settings.UseCamelCasingForNames, Is.False);
				Assert.That(settings.OptimizeForLargeData, Is.False);
				Assert.That(settings.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.Target_JavaScript | CrystalJsonSettings.OptionFlags.Layout_Indented));
				Assert.That(CrystalJsonSettings.JavaScriptIndented, Is.SameAs(settings));
			}

			// null
			using (Assert.EnterMultipleScope())
			{
				Assert.That(default(CrystalJsonSettings).IsJsonTarget(), Is.True);
				Assert.That(default(CrystalJsonSettings).IsJavascriptTarget(), Is.False);
				Assert.That(default(CrystalJsonSettings).IsCompactLayout(), Is.False);
				Assert.That(default(CrystalJsonSettings).IsFormattedLayout(), Is.True);
				Assert.That(default(CrystalJsonSettings).IsIndentedLayout(), Is.False);
			}

		}

		[Test]
		public void Test_JsonSettings_Flags()
		{

			var settings = CrystalJsonSettings.Json;
			Assert.That(settings.HideDefaultValues, Is.False);

			// Set options...

			settings = settings.WithoutDefaultValues();
			Assert.That(settings.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.HideDefaultValues));
			Assert.That(settings.HideDefaultValues, Is.True);

			Assert.That(settings.ShowNullMembers, Is.False);
			settings = settings.WithNullMembers();
			Assert.That(settings.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.HideDefaultValues | CrystalJsonSettings.OptionFlags.ShowNullMembers));
			Assert.That(settings.ShowNullMembers, Is.True);

			Assert.That(settings.UseCamelCasingForNames, Is.False);
			settings = settings.CamelCased();
			Assert.That(settings.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.HideDefaultValues | CrystalJsonSettings.OptionFlags.ShowNullMembers | CrystalJsonSettings.OptionFlags.UseCamelCasingForName));
			Assert.That(settings.UseCamelCasingForNames, Is.True);

			Assert.That(settings.OptimizeForLargeData, Is.False);
			settings = settings.ExpectLargeData();
			Assert.That(settings.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.HideDefaultValues | CrystalJsonSettings.OptionFlags.ShowNullMembers | CrystalJsonSettings.OptionFlags.UseCamelCasingForName | CrystalJsonSettings.OptionFlags.OptimizeForLargeData));
			Assert.That(settings.OptimizeForLargeData, Is.True);

			// Clear options...

			settings = settings.PascalCased();
			Assert.That(settings.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.HideDefaultValues | CrystalJsonSettings.OptionFlags.ShowNullMembers | CrystalJsonSettings.OptionFlags.OptimizeForLargeData));
			Assert.That(settings.UseCamelCasingForNames, Is.False);

			settings = settings.WithDefaultValues();
			Assert.That(settings.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.ShowNullMembers | CrystalJsonSettings.OptionFlags.OptimizeForLargeData));
			Assert.That(settings.HideDefaultValues, Is.False);

			settings = settings.OptimizedFor(largeData: false);
			Assert.That(settings.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.ShowNullMembers));
			Assert.That(settings.OptimizeForLargeData, Is.False);

			settings = settings.WithoutNullMembers();
			Assert.That(settings.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.None));
			Assert.That(settings.ShowNullMembers, Is.False);

			// should be now back to the default settings
			Assert.That(settings, Is.SameAs(CrystalJsonSettings.Json));
		}

		[Test]
		public void Test_JsonSettings_TextLayout()
		{
			var settings = CrystalJsonSettings.Json;
			Assert.That(settings.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.None));
			Assert.That(settings.TextLayout, Is.EqualTo(CrystalJsonSettings.Layout.Formatted));

			// Layout => Indented
			settings = settings.Indented();
			Assert.That(settings.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.Layout_Indented));
			Assert.That(settings.TextLayout, Is.EqualTo(CrystalJsonSettings.Layout.Indented));

			// Layout => Compact
			settings = settings.Compacted();
			Assert.That(settings.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.Layout_Compact));
			Assert.That(settings.TextLayout, Is.EqualTo(CrystalJsonSettings.Layout.Compact));

			// Layout => Formatted
			settings = settings.WithTextLayout(CrystalJsonSettings.Layout.Formatted);
			Assert.That(settings.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.None));
			Assert.That(settings.TextLayout, Is.EqualTo(CrystalJsonSettings.Layout.Formatted));

			// Should not override the other settings
			settings = settings.CamelCased();
			settings = settings.WithDateFormat(CrystalJsonSettings.DateFormat.TimeStampIso8601);
			settings = settings.Indented();
			Assert.That(settings.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.Layout_Indented | CrystalJsonSettings.OptionFlags.UseCamelCasingForName | CrystalJsonSettings.OptionFlags.DateFormat_TimeStampIso8601));
		}

		[Test]
		public void Test_JsonSettings_DateFormatting()
		{
			var settings = CrystalJsonSettings.Json;
			Assert.That(settings.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.None));
			Assert.That(settings.DateFormatting, Is.EqualTo(CrystalJsonSettings.DateFormat.Default));

			// DateFormat => Microsoft
			settings = settings.WithMicrosoftDates();
			Assert.That(settings.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.DateFormat_Microsoft));
			Assert.That(settings.DateFormatting, Is.EqualTo(CrystalJsonSettings.DateFormat.Microsoft));

			// DateFormat => JavaScript
			settings = settings.WithJavaScriptDates();
			Assert.That(settings.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.DateFormat_JavaScript));
			Assert.That(settings.DateFormatting, Is.EqualTo(CrystalJsonSettings.DateFormat.JavaScript));

			// DateFormat => Iso
			settings = settings.WithIso8601Dates();
			Assert.That(settings.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.DateFormat_TimeStampIso8601));
			Assert.That(settings.DateFormatting, Is.EqualTo(CrystalJsonSettings.DateFormat.TimeStampIso8601));

			// DateFormat => Default
			settings = settings.WithDateFormat(CrystalJsonSettings.DateFormat.Default);
			Assert.That(settings.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.None));
			Assert.That(settings.DateFormatting, Is.EqualTo(CrystalJsonSettings.DateFormat.Default));

			// Should not override the other settings
			settings = settings.CamelCased();
			settings = settings.WithInterning(CrystalJsonSettings.StringInterning.IncludeValues);
			settings = settings.WithIso8601Dates();
			Assert.That(settings.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.DateFormat_TimeStampIso8601 | CrystalJsonSettings.OptionFlags.StringInterning_IncludeValues | CrystalJsonSettings.OptionFlags.UseCamelCasingForName));
		}

		[Test]
		public void Test_JsonSettings_StringInterning()
		{
			var settings = CrystalJsonSettings.Json;
			Assert.That(settings.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.None));
			Assert.That(settings.InterningMode, Is.EqualTo(CrystalJsonSettings.StringInterning.Default));

			// InterningMode => Disabled
			settings = settings.DisableInterning();
			Assert.That(settings.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.StringInterning_Disabled));
			Assert.That(settings.InterningMode, Is.EqualTo(CrystalJsonSettings.StringInterning.Disabled));

			// InterningMode => IncludeNumbers
			settings = settings.WithInterning(CrystalJsonSettings.StringInterning.IncludeNumbers);
			Assert.That(settings.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.StringInterning_IncludeNumbers));
			Assert.That(settings.InterningMode, Is.EqualTo(CrystalJsonSettings.StringInterning.IncludeNumbers));

			// InterningMode => IncludeValues
			settings = settings.WithInterning(CrystalJsonSettings.StringInterning.IncludeValues);
			Assert.That(settings.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.StringInterning_IncludeValues));
			Assert.That(settings.InterningMode, Is.EqualTo(CrystalJsonSettings.StringInterning.IncludeValues));

			// InterningMode => Default
			settings = settings.WithInterning(CrystalJsonSettings.StringInterning.Default);
			Assert.That(settings.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.None));
			Assert.That(settings.InterningMode, Is.EqualTo(CrystalJsonSettings.StringInterning.Default));

			// Should not override the other settings
			settings = settings.CamelCased();
			settings = settings.WithDateFormat(CrystalJsonSettings.DateFormat.TimeStampIso8601);
			settings = settings.DisableInterning();
			Assert.That(settings.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.StringInterning_Disabled | CrystalJsonSettings.OptionFlags.UseCamelCasingForName | CrystalJsonSettings.OptionFlags.DateFormat_TimeStampIso8601));
		}

		[Test]
		public void Test_JsonSettings_Immutability()
		{
			var a = CrystalJsonSettings.Json;
			Assert.That(a, Is.Not.Null);
			Assert.That(a.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.None));

			var b = a.WithEnumAsStrings();
			Assert.That(b, Is.Not.Null.And.Not.SameAs(a));
			Assert.That(b.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.EnumsAsString));
			Assert.That(a.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.None));

			var c = a.WithEnumAsNumbers();
			Assert.That(c, Is.SameAs(a));
			Assert.That(c.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.None));
			Assert.That(b.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.EnumsAsString));
		}

	}

}
