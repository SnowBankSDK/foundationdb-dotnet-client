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


namespace SnowBank.Testing.Framework.Tests
{
	using System.IO;
	using System.Net;
	using Microsoft.AspNetCore.Builder;
	using Microsoft.AspNetCore.Http;
	using NUnit.Framework;

	[TestFixture]
	public class HttpHelperFacts : DistributedTest
	{

		private async Task<DistributedTestComponent> StartEchoHost()
		{
			var context = await MakeItSo(env => env.AddSimpleLan(lan =>
			{
				lan.WithMinimalWebHost("WEB", host =>
				{
					host.ConfigureApplication(app =>
					{
						app.MapGet("/json", () => new { hello = "world" });
						app.MapPost("/echo", async (HttpContext ctx) => $"{ctx.Request.Method} {await new StreamReader(ctx.Request.Body).ReadToEndAsync(ctx.RequestAborted)}");
					});
				});
			}));
			return context.GetWebHost("WEB");
		}

		[Test]
		public async Task Test_Local_PostText_Sends_A_Post_With_The_Body()
		{
			var web = await StartEchoHost();

			var (status, body) = await web.Http.Local.PostTextAsync("/echo", "hello", ct: this.Cancellation);
			Assert.That(status, Is.EqualTo(HttpStatusCode.OK));
			Assert.That(body, Is.EqualTo("POST hello"));
		}

#pragma warning disable CS0618 // the obsolete helpers are the subject of these tests

		[Test]
		public async Task Test_Obsolete_Helpers_Send_A_Relative_Path_To_The_Host_Itself()
		{
			var web = await StartEchoHost();

			var (status, json) = await web.HttpGetJsonAsync("/json", ct: this.Cancellation);
			Assert.That(status, Is.EqualTo(HttpStatusCode.OK));
			Assert.That(json["hello"].ToString(), Is.EqualTo("world"));

			var (textStatus, text) = await web.HttpPostTextAsync("/echo", "hello", ct: this.Cancellation);
			Assert.That(textStatus, Is.EqualTo(HttpStatusCode.OK));
			Assert.That(text, Is.EqualTo("POST hello"));

			(textStatus, text) = await web.HttpGetTextAsync(web.GetUri("/json").ToString(), ct: this.Cancellation);
			Assert.That(textStatus, Is.EqualTo(HttpStatusCode.OK), "an absolute URI still works");
		}

#pragma warning restore CS0618

	}

}
