#region Copyright (c) 2023-2024 SnowBank SAS, (c) 2005-2023 Doxense SAS
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

// ReSharper disable MethodHasAsyncOverload

namespace FoundationDB.Samples.Tutorials
{

	public class ClassScheduling : IAsyncTest
	{

		public ClassScheduling()
		{
			// create a bunch of random classes
			string[] levels = [ "intro", "for dummies", "remedial", "101", "201", "301", "mastery", "lab", "seminar" ];
			string[] types = [ "chem", "bio", "cs", "geometry", "calc", "alg", "film", "music", "art", "dance" ];
			var times = Enumerable.Range(2, 20).Select(h => string.CreateInvariant($"{h}:00")).ToArray();

			this.ClassNames = times
				.SelectMany((h) => types.Select(t => $"{h} {t}"))
				.SelectMany((s) => levels.Select((l) => $"{s} {l}"))
				.ToArray();
		}

		public string[] ClassNames { get; }

		public IKeySubspace? Subspace { get; private set; }

		protected FdbTupleKey<string, string> ClassKey(string c)
		{
			return this.Subspace!.Key("class", c);
		}

		protected FdbTupleKey<string, string, string> AttendsKey(string s, string c)
		{
			return this.Subspace!.Key("attends", s, c);
		}

		protected FdbKeyRange<FdbTupleKey<string, string>, FdbTupleKey<string, string>> AttendsKeys(string s)
		{
			return this.Subspace!.Key("attends", s).ToRange();
		}

		/// <summary>
		/// Setup the initial state of the database
		/// </summary>
		public async Task Init(IFdbDatabase db, CancellationToken ct)
		{
			// open the folder where we will store everything
			this.Subspace = await db.ReadWriteAsync(async tr =>
			{
				var subspace = await db.Root["Tutorials"]["ClassScheduling"].CreateOrOpenAsync(tr);

				// clear all previous values
				tr.ClearRange(subspace);

				// insert all the classes
				foreach (var c in this.ClassNames)
				{
					tr.Set(ClassKey(c), Slice.FromStringAscii("100"));
				}

				return subspace;
			}, ct);
		}

		/// <summary>
		/// Returns the list of names of all existing classes
		/// </summary>
		public Task<List<string>> AvailableClasses(IFdbReadOnlyTransaction tr)
		{
			return tr.GetRange(this.Subspace!.Key("class").ToRange())
				.Where(kvp => int.TryParse(kvp.Value.Span, out _)) // (step 3)
				.Select(kvp => this.Subspace!.DecodeLast<string>(kvp.Key)!)
				.ToListAsync();
		}

		/// <summary>
		/// Signup a student to a class
		/// </summary>
		public async Task Signup(IFdbTransaction tr, string s, string c)
		{
			var rec = AttendsKey(s, c);

			if ((await tr.GetAsync(rec)).IsPresent)
			{ // already signed up
				return;
			}
			int seatsLeft = int.Parse((await tr.GetAsync(ClassKey(c))).ToStringAscii()!);
			if (seatsLeft <= 0)
			{
				throw new InvalidOperationException("No remaining seats");
			}

			var classes = await tr.GetRange(AttendsKeys(s)).ToListAsync();
			if (classes.Count >= 5) throw new InvalidOperationException("Too many classes");

			tr.Set(ClassKey(c), Slice.FromStringAscii((seatsLeft - 1).ToString()));
			tr.Set(rec, Slice.Empty);
		}

		/// <summary>
		/// Drop a student from a class
		/// </summary>
		public async Task Drop(IFdbTransaction tr, string s, string c)
		{
			var rec = AttendsKey(s, c);
			if ((await tr.GetAsync(rec)).IsNullOrEmpty)
			{ // not taking this class
				return;
			}

			var students = int.Parse((await tr.GetAsync(ClassKey(c))).ToStringAscii()!);
			tr.Set(ClassKey(c), Slice.FromStringAscii((students + 1).ToString()));
			tr.Clear(rec);
		}

		/// <summary>
		/// Drop a student from a class, and sign him up to another class
		/// </summary>
		public async Task Switch(IFdbTransaction tr, string s, string oldC, string newC)
		{
			await Drop(tr, s, oldC);
			await Signup(tr, s, newC);
		}

		/// <summary>
		/// Simulate a student that is really indecisive
		/// </summary>
		public async Task IndecisiveStudent(IFdbDatabase db, int id, int ops, CancellationToken ct)
		{
			string student = "s" + id.ToString("D04");
			var allClasses = new List<string>(this.ClassNames);
			var myClasses = new List<string>();

			var rnd = new Random(id * 7);

			for (int i = 0; i < ops && !ct.IsCancellationRequested; i++)
			{
				int classCount = myClasses.Count;

				var moods = new List<string>();
				if (classCount > 0) moods.AddRange([ "drop", "switch" ]);
				if (classCount < 5) moods.Add("add");
				string mood = moods[rnd.Next(moods.Count)];

				try
				{
					allClasses ??= await db.ReadAsync(AvailableClasses, ct);

					switch (mood)
					{
						case "add":
						{
							string @class = allClasses[rnd.Next(allClasses.Count)];
							await db.WriteAsync((tr) => Signup(tr, student, @class), ct);
							myClasses.Add(@class);
							break;
						}
						case "drop":
						{
							string @class = allClasses[rnd.Next(allClasses.Count)];
							await db.WriteAsync((tr) => Drop(tr, student, @class), ct);
							myClasses.Remove(@class);
							break;
						}
						case "switch":
						{
							string oldClass = allClasses[rnd.Next(allClasses.Count)];
							string newClass = allClasses[rnd.Next(allClasses.Count)];
							await db.WriteAsync((tr) => Switch(tr, student, oldClass, newClass), ct);
							myClasses.Remove(oldClass);
							myClasses.Add(newClass);
							break;
						}
						default:
						{
							throw new InvalidOperationException("Unsupported mood value.");
						}
					}
				}
				catch (Exception e)
				{
					if (e is TaskCanceledException or OperationCanceledException) throw;
					allClasses = null;
				}
			}

			ct.ThrowIfCancellationRequested();

		}

		#region IAsyncTest...

		public string Name => "ClassScheduling";

		public async Task Run(IFdbDatabase db, TextWriter log, CancellationToken ct)
		{
			const int STUDENTS = 10;
			const int OPS_PER_STUDENTS = 10;

			await Init(db, ct);
			log.WriteLine("# Class scheduling test initialized");

			// run multiple students
			var elapsed = await Program.RunConcurrentWorkersAsync(
				STUDENTS,
				(i, cancel) => IndecisiveStudent(db, i, OPS_PER_STUDENTS, cancel),
				ct
			);

			log.WriteLine("# Ran {0} transactions in {1:0.0##} sec", (STUDENTS * OPS_PER_STUDENTS), elapsed.TotalSeconds);
		}

		#endregion

	}
}
