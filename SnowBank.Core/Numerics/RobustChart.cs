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

namespace SnowBank.Numerics
{
	using System.Globalization;
	using System.Text;

	/// <summary>Generates ASCII Art that look like plots</summary>
	[PublicAPI]
	public static class RobustChart
	{

		#region Samples
		//
		// Page counter (will null values marked as 'X'):
		//
		// 1,156,800 +--------------+--------------+--------------+-------------+
		//           |              :              :              :             |
		//           |              :              :              :        ######
		//           |              :########################## ###########     |
		// 1,156,650 +.............##..............:..............:.............+
		//           |           ## :              :              :             |
		//           |          #   :              :              :             |
		//           |         #    :              :              :             |
		//           |              :              :              :             |
		// 1,156,500 +.......#.....................:............................+
		//           |      #       :              :              :             |
		//           |     #        :              :              :             |
		//           |    #         :              :              :             |
		//           |   #          :              :              :             |
		// 1,156,350 +..............:..............:..............:.............+
		//           |  #           :              :              :             |
		//           | #            :              :              :             |
		//           |#             :              :              :             |
		//           #              :              :              :             |
		// 1,156,200 +--------X-----+--------------+-----------X--+-------------+
		//
		// Load avarage (0..1):
		//
		//       0.4 +--------------+--------------+--------------+-------------+
		//           |              :              :              :             |
		//           |              :              :              :             |
		//           |              :   #          :              :             |
		//       0.3 +..............:..............:..............:.............+
		//           |  #           :              :              :             |
		//           |              :              :              :      #    # |
		//           |              :              :              :             |
		//           |              :              :              :             |
		//       0.2 +#........##..................:...#.....#..................+
		//           |    ##        #     #        :    #         :   #     #   |
		//           |              :              :              :             |
		//           |   #    #     :    #        #:          #   : #  #        |
		//           | #    #       : #     ###    :# #  #  #   # :        #   #|
		//       0.1 +...........###:#.#.........#.:.#....##...#.##.....#.......+
		//           |              :              :              :             |
		//           #              :      #   #   #              :#      #  #  |
		//           |              :           #  :              :  #          #
		//           |       #      :              :              :             |
		//       0.0 +--------------+--------------+--------------+-------------+
		//
		#endregion
		
		/// <summary>Buffer use for rendering a frame</summary>
		[DebuggerDisplay("Size={Width}x{Height}")]
		[PublicAPI]
		public readonly struct FrameBuffer : IFormattable
		{

			// Frame that contains a "texel" grid (~ pixel approximated by an ASCII character)
			// - the X axis goes from left to right
			// - the Y axis goes from bottom to top (to match the conventions when plotting charts, reversed compared to bitmap or computer graphics)
			// => (0, 0) maps to the texel at the bottom left of the frame, and (Width-1, Height-1) maps to the texel at the top right of the frame.

			/// <summary>Width of the buffer (in texels)</summary>
			public readonly int Width;

			/// <summary>Height of the buffer (in texels)</summary>
			public readonly int Height;

			/// <summary>Flatten texels array, grouped by row, with top-most row first, and the bottom-most row last)</summary>
			/// <remarks>The texel (X, Y) is located at offset (HEIGHT - Y - 1) * WIDTH + X</remarks>
			public readonly char[] Buffer;

			/// <summary>Constructs a new <see cref="FrameBuffer"/></summary>
			public FrameBuffer(int width, int height)
			{
				Contract.GreaterThan(width, 0);
				Contract.GreaterThan(height, 0);

				this.Width = width;
				this.Height = height;
				var buffer = new char[width * height];
				for (int i = 0; i < buffer.Length; i++) buffer[i] = ' ';
				this.Buffer = buffer;
			}

			/// <summary>Constructs a new <see cref="FrameBuffer"/></summary>
			public FrameBuffer(char[] buffer, int width, int height)
			{
				Contract.NotNull(buffer);
				Contract.GreaterThan(width, 0);
				Contract.GreaterThan(height, 0);
				if (buffer.Length != width * height) throw new InvalidOperationException("Buffer size must be equal to width times height.");

				this.Width = width;
				this.Height = height;
				this.Buffer = buffer;
			}

			/// <summary>Maps a X coordinates into the buffer space</summary>
			/// <param name="x"><c>0</c> &lt;= x &lt; <see cref="Width"/></param>
			/// <returns>Rounded integer, that fits inside the buffer</returns>
			[Pure]
			private int RoundX(double x) => Math.Min(this.Width - 1, Math.Max(0, (int) Math.Floor(x)));

			/// <summary>Maps a X coordinates into the buffer space</summary>
			/// <param name="y"><c>0</c> &lt;= y &lt; <see cref="Height"/> (0 = bottom)</param>
			/// <returns>Rounded integer, that fits inside the buffer, reversed with (0 on top)</returns>
			[Pure]
			private int RoundY(double y) => this.Height - 1 - Math.Min(this.Height - 1, Math.Max(0, (int)Math.Floor(y)));

			/// <summary>Clips the X coordinate to fit within the width of the frame</summary>
			/// <returns>0 if <paramref name="x"/> &lt; 0, or WIDTH-1 if <paramref name="x"/> &gt;= WIDTH</returns>
			[Pure]
			private int BoundX(int x) => Math.Min(this.Width - 1, Math.Max(0, x));

			/// <summary>Clips the Y coordinate to fit within the height of the frame</summary>
			/// <returns>0 if <paramref name="y"/> &lt; 0, or HEIGHT-1 si <paramref name="y"/> &gt;= HEIGHT</returns>
			[Pure]
			private int BoundY(int y) => this.Height - 1 - Math.Min(this.Height - 1, Math.Max(0, y));

			/// <summary>Sets the value of the texel at the given coordinates</summary>
			/// <param name="x">X coordinate (0 = left)</param>
			/// <param name="y">Y coordinate (0 = bottom)</param>
			/// <param name="c">"Color" of this texel</param>
			public void Set(double x, double y, char c)
			{
				int xx = RoundX(x);
				int yy = RoundY(y);
				int w = this.Width;
				if (Math.Abs(xx) < w && Math.Abs(yy) < this.Height)
				{
					this.Buffer[yy * w + xx] = c;
				}
			}

			/// <summary>Sets the value of the texel at the given coordinates</summary>
			/// <param name="x">X coordinate (0 = left)</param>
			/// <param name="y">Y coordinate (0 = bottom)</param>
			/// <param name="c">"Color" of this texel</param>
			public void Set(int x, int y, char c)
			{
				int w = this.Width;
				y = this.Height - 1 - y;
				if (Math.Abs(x) < w && Math.Abs(y) < this.Height)
				{
					this.Buffer[y * w + x] = c;
				}
			}

			/// <summary>Draws a horizontal line</summary>
			/// <param name="x0">Left X coordinate</param>
			/// <param name="y0">Y coordinate</param>
			/// <param name="x1">Right X coordinate</param>
			/// <param name="c">"Color" of this line</param>
			public void HorizontalLine(double x0, double y0, double x1, char c = '.')
			{
				int cx0 = RoundX(x0);
				int cx1 = RoundX(x1);
				if (cx0 <= cx1)
				{
					int cy0 = RoundY(y0);
					int p = cy0 * this.Width;
					var buffer = this.Buffer;
					for (int i = cx0; i <= cx1; i++)
					{
						buffer[p + i] = c;
					}
				}
			}

			/// <summary>Draws a vertical line</summary>
			/// <param name="x0">X coordinate</param>
			/// <param name="y0">Bottom Y coordinate</param>
			/// <param name="y1">Top Y coordinate</param>
			/// <param name="c">"Color" of this line</param>
			public void VerticalLine(double x0, double y0, double y1, char c = ':')
			{
				int cy0 = RoundY(y0);
				int cy1 = RoundY(y1);
				if (cy0 >= cy1) //note: y is reversed!
				{
					int cx0 = RoundX(x0);
					int w = this.Width;
					var buffer = this.Buffer;
					for (int i = cy1; i <= cy0; i++) //note: y is reversed!
					{
						buffer[i * w + cx0] = c;
					}
				}
			}

			/// <summary>Writes a text label</summary>
			public void DrawText(int x, int y, string text, int align = 0)
			{
				int l = align > 0 ? align - text.Length : 0;
				for (int i = 0; i < text.Length; i++)
				{
					Set(x + i + l, y, text[i]);
				}
			}

			/// <summary>Copies another frame into this frame</summary>
			/// <param name="x">X coordinate (left) where to draw the image</param>
			/// <param name="y">Y coordinate (bottom) where to draw the image</param>
			/// <param name="image">Image to copy (truncated to fit)</param>
			public void DrawFrame(int x, int y, FrameBuffer image)
			{
				Contract.Positive(x);
				Contract.Positive(y);
				if (x >= this.Width || y >= this.Height) return;
				int w = Math.Min(this.Width - x, image.Width);
				int h = Math.Min(this.Height - y, image.Height);

				var dst = this.Buffer;
				var src = image.Buffer;
				for (int j = 0; j < h; j++)
				{
					int offSrc = (image.Height - y - j - 1) * image.Width;
					int offDest = (this.Height - j - 1) * this.Width;
					for (int i = 0; i < w; i++)
					{
						dst[offDest + x + i] = src[offSrc + i];
					}
				}
			}

			/// <summary>Draws the X axis (horizontal)</summary>
			public void DrawXAxis(int w, int x0 = 0, int y0 = 0, char c = '-', char start = '+', char end = '>')
			{
				HorizontalLine(x0, y0, x0 + w, c);
				if (w > 2 && start != '\0') Set(x0, y0, start);
				if (w > 1 && end != 0) Set(x0 + w - 1, y0, end);
			}

			/// <summary>Draws the vertical grid lines for the X axis</summary>
			public void DrawXGridLine(int w, int x0 = 0, int y0 = 0, char c = '.', char start = '+', char end = '\0')
			{
				if (w > 1) Set(x0, y0, start != '\0' ? start : c);
				if (w > 2) HorizontalLine(x0 + 1, y0, x0 + w - 2, c);
				if (w > 0) Set(x0 + w - 1, y0, end != '\0' ? end : c);
			}

			/// <summary>Draws the Y axis (vertical)</summary>
			public void DrawYAxis(int h, int x0 = 0, int y0 = 0, char c = '|', char start = '+', char end = '^')
			{
				VerticalLine(x0, y0, y0 + h, c);
				if (h > 2 && start != '\0') Set(x0, y0, start);
				if (h > 1 && end != '\0') Set(x0, y0 + h - 1, end);
			}

			/// <summary>Draws the horizontal grid lines for the Y axis</summary>
			public void DrawYGridLine(int h, int x0 = 0, int y0 = 0, char c = ':', char start = '+', char end = '\0')
			{
				if (h > 1) Set(x0, y0, start != '\0' ? start : c);
				if (h > 2) VerticalLine(x0, y0 + 1, y0 + h - 2, c);
				if (h > 0) Set(x0, y0 + h - 1, end != '\0' ? end : c);
			}

			/// <summary>Stitch to frames horizontally</summary>
			/// <param name="left">Frame that goes to the left</param>
			/// <param name="right">Frame that goes to the right</param>
			/// <param name="pad">Optional padding inserted between the frames</param>
			/// <returns>Frame with both frames stacked horizontally</returns>
			/// <exception cref="InvalidOperationException"> the frames have different heights</exception>
			[Pure]
			public static FrameBuffer CombineHorizontal(FrameBuffer left, FrameBuffer right, int pad = 0)
			{
				if (left.Height != right.Height) throw new InvalidOperationException("Both frames must have the same height");
				var frame = new FrameBuffer(left.Width + pad + right.Width, left.Height);
				frame.DrawFrame(0, 0, left);
				frame.DrawFrame(pad + left.Width, 0, right);
				return frame;
			}

			/// <summary>Stitch to frames vertically</summary>
			/// <param name="top">Frame that goes to the top</param>
			/// <param name="bottom">Frame that goes to the bottom</param>
			/// <param name="pad">Optional padding inserted between the frames</param>
			/// <returns>Frame with both frames stacked vertically</returns>
			/// <exception cref="InvalidOperationException"> the frames have different widths</exception>
			[Pure]
			public static FrameBuffer CombineVertical(FrameBuffer top, FrameBuffer bottom, int pad = 0)
			{
				if (top.Width != bottom.Width) throw new InvalidOperationException("Both frames must have the same width");
				var frame = new FrameBuffer(top.Width, top.Height + pad + bottom.Height);
				frame.DrawFrame(0, bottom.Height + pad, top);
				frame.DrawFrame(0, 0, bottom);
				return frame;
			}

			/// <summary>Output the frame into a <see cref="StringBuilder"/></summary>
			/// <param name="output">Destination buffer</param>
			/// <param name="prefix">Text added to the start of each line</param>
			/// <param name="suffix">Text added to the end of each line</param>
			public void Output(StringBuilder output, string? prefix = null, string suffix = "\r\n")
			{
				Contract.Debug.Requires(output != null);
				int w = this.Width, h = this.Height, ofs = 0;
				var buffer = this.Buffer;
				for (int i = 0; i < h; i++)
				{
					output.Append(prefix).Append(buffer, ofs, w).Append(suffix);
					ofs += w;
				}
			}

			/// <summary>Renders the frame into a multi-line string</summary>
			public override string ToString()
			{
				var sb = new StringBuilder((this.Width + 2) * this.Height);
				Output(sb);
				return sb.ToString();
			}

			/// <summary>Renders the frame into a multi-line string</summary>
			public string ToString(string? format, IFormatProvider? formatProvider = null)
			{
				switch (format ?? "D")
				{
					case "D": case "d":
						return ToString();
					default:
						throw new ArgumentException("Invalid format specified.", nameof(format));
				}
			}
		}

		/// <summary>Series of data</summary>
		[DebuggerDisplay("Count={Count}; Name={Name}")]
		[PublicAPI]
		public readonly struct Data
		{

			/// <summary>Sampled values</summary>
			public readonly double?[] Values;

			/// <summary>Name of the series</summary>
			public readonly string? Name;

			#region Constructors...

			/// <summary>Constructs a new series of data</summary>
			public Data(double?[] values, string? name = null)
			{
				this.Values = values;
				this.Name = null;
			}

			/// <summary>Creates a series of data from raw values</summary>
			[Pure]
			public static Data Convert<T>(IEnumerable<T> values, [InstantHandle] Func<T, double> transform, string? name = null)
			{
				Contract.NotNull(values);
				Contract.NotNull(transform);

				if (values is ICollection<T> coll)
				{
					var xs = new double?[coll.Count];
					int p = 0;
					foreach (var x in values)
					{
						xs[p++] = transform(x);
					}
					return new Data(xs, name);
				}
				else
				{
					var xs = new List<double?>();
					foreach (var x in values)
					{
						xs.Add(transform(x));
					}
					return new Data(xs.ToArray(), name);
				}
			}

			/// <summary>Creates a series of data from raw values</summary>
			[Pure]
			public static Data Convert<T>(IEnumerable<T> values, [InstantHandle] Func<T, double?> transform, string? name = null)
			{
				Contract.NotNull(values);
				Contract.NotNull(transform);

				if (values is ICollection<T> coll)
				{
					var xs = new double?[coll.Count];
					int p = 0;
					foreach (var x in values)
					{
						xs[p++] = transform(x);
					}
					return new Data(xs, name);
				}

				return new Data(values.Select(transform).ToArray(), name);
			}

			/// <summary>Creates a series of data from raw values</summary>
			[Pure]
			public static Data Convert(int[] values, string? name = null)
			{
				var xs = new double?[values.Length];
				for (int i = 0; i < values.Length; i++)
				{
					xs[i] = values[i];
				}
				return new Data(xs, name);
			}

			/// <summary>Creates a series of data from raw values</summary>
			[Pure]
			public static Data Convert(IEnumerable<int> values, string? name = null)
			{
				return new Data(values.Select(x => (double?) x).ToArray(), name);
			}

			/// <summary>Creates a series of data from raw values</summary>
			[Pure]
			public static Data Convert(int?[] values, string? name = null)
			{
				var xs = new double?[values.Length];
				for (int i = 0; i < values.Length; i++)
				{
					xs[i] = values[i];
				}
				return new Data(xs, name);
			}

			/// <summary>Creates a series of data from raw values</summary>
			[Pure]
			public static Data Convert(IEnumerable<int?> values, string? name = null)
			{
				return new Data(values.Select(x => (double?) x).ToArray(), name);
			}

			/// <summary>Creates a series of data from raw values</summary>
			[Pure]
			public static Data Convert(uint[] values, string? name = null)
			{
				var xs = new double?[values.Length];
				for (int i = 0; i < values.Length; i++)
				{
					xs[i] = values[i];
				}
				return new Data(xs, name);
			}

			/// <summary>Creates a series of data from raw values</summary>
			[Pure]
			public static Data Convert(IEnumerable<uint> values, string? name = null)
			{
				return new Data(values.Select(x => (double?) x).ToArray(), name);
			}

			/// <summary>Creates a series of data from raw values</summary>
			[Pure]
			public static Data Convert(uint?[] values, string? name = null)
			{
				var xs = new double?[values.Length];
				for (int i = 0; i < values.Length; i++)
				{
					xs[i] = values[i];
				}
				return new Data(xs, name);
			}

			/// <summary>Creates a series of data from raw values</summary>
			[Pure]
			public static Data Convert(IEnumerable<uint?> values, string? name = null)
			{
				return new Data(values.Select(x => (double?) x).ToArray(), name);
			}

			/// <summary>Creates a series of data from raw values</summary>
			[Pure]
			public static Data Convert(long[] values, string? name = null)
			{
				var xs = new double?[values.Length];
				for (int i = 0; i < values.Length; i++)
				{
					xs[i] = values[i];
				}
				return new Data(xs, name);
			}

			/// <summary>Creates a series of data from raw values</summary>
			[Pure]
			public static Data Convert(IEnumerable<long> values, string? name = null)
			{
				return new Data(values.Select(x => (double?) x).ToArray(), name);
			}

			/// <summary>Creates a series of data from raw values</summary>
			[Pure]
			public static Data Convert(long?[] values, string? name = null)
			{
				var xs = new double?[values.Length];
				for (int i = 0; i < values.Length; i++)
				{
					xs[i] = values[i];
				}
				return new Data(xs, name);
			}

			/// <summary>Creates a series of data from raw values</summary>
			[Pure]
			public static Data Convert(IEnumerable<long?> values, string? name = null)
			{
				return new Data(values.Select(x => (double?) x).ToArray(), name);
			}

			/// <summary>Creates a series of data from raw values</summary>
			[Pure]
			public static Data Convert(ulong[] values, string? name = null)
			{
				var xs = new double?[values.Length];
				for (int i = 0; i < values.Length; i++)
				{
					xs[i] = values[i];
				}
				return new Data(xs, name);
			}

			/// <summary>Creates a series of data from raw values</summary>
			[Pure]
			public static Data Convert(IEnumerable<ulong> values, string? name = null)
			{
				return new Data(values.Select(x => (double?) x).ToArray(), name);
			}

			/// <summary>Creates a series of data from raw values</summary>
			[Pure]
			public static Data Convert(ulong?[] values, string? name = null)
			{
				var xs = new double?[values.Length];
				for (int i = 0; i < values.Length; i++)
				{
					xs[i] = values[i];
				}
				return new Data(xs, name);
			}

			/// <summary>Creates a series of data from raw values</summary>
			[Pure]
			public static Data Convert(IEnumerable<ulong?> values, string? name = null)
			{
				return new Data(values.Select(x => (double?) x).ToArray(), name);
			}

			/// <summary>Creates a series of data from raw values</summary>
			[Pure]
			public static Data Convert(double[] values, string? name = null)
			{
				var xs = new double?[values.Length];
				for (int i = 0; i < values.Length; i++)
				{
					xs[i] = values[i];
				}
				return new Data(xs, name);
			}

			/// <summary>Creates a series of data from raw values</summary>
			[Pure]
			public static Data Convert(IEnumerable<double> values, string? name = null)
			{
				return new Data(values.Select(x => (double?) x).ToArray(), name);
			}

			/// <summary>Creates a series of data from raw values</summary>
			[Pure]
			public static Data Convert(double?[] values, string? name = null)
			{
				var xs = new double?[values.Length];
				Array.Copy(values, 0, xs, 0, xs.Length);
				return new Data(xs, name);
			}

			/// <summary>Creates a series of data from raw values</summary>
			[Pure]
			public static Data Convert(IEnumerable<double?> values, string? name = null)
			{
				return new Data(values.ToArray(), name);
			}

			/// <summary>Transforms the data of this series into a new series</summary>
			public Data Convert(Func<double?, double?> transform)
			{
				return new Data(this.Values.Select(transform).ToArray(), this.Name);
			}

			/// <summary>Creates a new series by computing the natural log of this series</summary>
			public Data Log()
			{
				return new Data(this.Values.Select(x => x.HasValue ? Math.Log(x.Value) : default(double?)).ToArray(), this.Name);
			}

			/// <summary>Creates a new series by computing the log base 10 of this series</summary>
			public Data Log10()
			{
				return new Data(this.Values.Select(x => x.HasValue ? Math.Log10(x.Value) : default(double?)).ToArray(), this.Name);
			}

			#endregion

			/// <summary>Number of sampled values</summary>
			public int Count => this.Values.Length;

			/// <summary>Computes the minimum value in the series</summary>
			/// <remarks>Returns <c>null</c> if all the samples are <c>null</c></remarks>
			public double? Min()
			{
				var min = default(double?);
				foreach (var v in this.Values)
				{
					if (v != null && (min == null || min.Value > v.Value)) min = v;
				}
				return min;
			}

			/// <summary>Computes the maximum value in the series</summary>
			/// <remarks>Returns <c>null</c> if all the samples are <c>null</c></remarks>
			public double? Max()
			{
				var max = default(double?);
				foreach (var v in this.Values)
				{
					if (v != null && (max == null || max.Value < v.Value)) max = v;
				}
				return max;
			}

			/// <summary>Tests if at least one sample is <c>null</c></summary>
			public bool HasNulls()
			{
				foreach (var v in this.Values)
				{
					if (v == null) return true;
				}
				return false;
			}

			/// <summary>Generates a new series with the derivative of this series</summary>
			/// <returns>New instance of size N-1, with RESULT[i] = THIS[i+1] - THIS[i]</returns>
			[Pure]
			public Data Derive()
			{
				var vals = this.Values;
				var d = new double?[vals.Length - 1];
				for (int i = 1; i < vals.Length; i++)
				{
					d[i - 1] = vals[i] - vals[i - 1];
				}
				return new Data(d, this.Name != null ? (this.Name + " (d/dt)") : null);
			}

			/// <summary>Replaces all 0 values with <c>null</c></summary>
			[Pure]
			public Data WithoutZero()
			{
				var vals = this.Values;
				var d = new double?[vals.Length];
				for (int i = 0; i < vals.Length; i++)
				{
					if (vals[i] > 0) d[i] = vals[i];
				}
				return new Data(d, this.Name);
			}
		}

		/// <summary>Helper class for generating axis</summary>
		[PublicAPI]
		public static class Axis
		{

			/// <summary>Performs a linear transformation</summary>
			[Pure]
			public static Func<double, double> Linear(double min, double max, double range)
			{
				// ReSharper disable once CompareOfFloatsByEqualityOperator
				double r = max == min ? 1 : (range / (max - min));
				return (x) => (x - min) * r;
			}

		}

		/// <summary>Draws a series of data onto a frame</summary>
		/// <param name="frame">Frame where to draw</param>
		/// <param name="xs">Series of data</param>
		/// <param name="xAxis">Scaler for the X axis</param>
		/// <param name="yAxis">Scaler for the Y axis</param>
		/// <param name="x0">X coordinate of the origin</param>
		/// <param name="y0">Y coordinate of the origin</param>
		/// <param name="c">"Color" for the points</param>
		/// <param name="nc">"Color" for the missing values</param>
		public static void DrawData(FrameBuffer frame, Data xs, [InstantHandle] Func<double, double> xAxis, [InstantHandle] Func<double, double> yAxis, int x0 = 0, int y0 = 0, char c = '*', char nc = 'x')
		{
			var values = xs.Values;
			for (int x = 0; x < values.Length; x++)
			{
				var y = values[x];
				if (y != null)
				{
					frame.Set(x0 + xAxis(x), y0 + yAxis(y.Value), c);
				}
				else if (nc != '\0')
				{
					frame.Set(x0 + xAxis(x), y0, nc);
				}
			}
		}

		/// <summary>Draws an X/Y plot onto a frame</summary>
		/// <param name="frame">Frame where to draw</param>
		/// <param name="xs">Series of data for the X coordinates</param>
		/// <param name="ys">Series of data for the Y coordinates</param>
		/// <param name="xAxis">Scaler for the X axis</param>
		/// <param name="yAxis">Scaler for the Y axis</param>
		/// <param name="x0">X coordinate of the origin</param>
		/// <param name="y0">Y coordinate of the origin</param>
		/// <param name="c">"Color" for the points</param>
		/// <param name="nc">"Color" for the missing values</param>
		public static void DrawData(FrameBuffer frame, Data xs, Data ys, [InstantHandle] Func<double, double> xAxis, [InstantHandle] Func<double, double> yAxis, int x0 = 0, int y0 = 0, char c = '*', char nc = 'x')
		{
			int n = xs.Count;
			for (int idx = 0; idx < n; idx++)
			{
				var x = xs.Values[idx];
				if (x == null) continue;
				var y = ys.Values[idx];
				if (y != null)
				{
					frame.Set(x0 + xAxis(x.Value), y0 + yAxis(y.Value), c);
				}
				else if (nc != '\0')
				{
					frame.Set(x0 + xAxis(x.Value), y0, nc);
				}
			}
		}

		/// <summary>Rounds a value down to the specified number of digits</summary>
		/// <param name="x">Value to round down</param>
		/// <param name="digits">If positive, number of decimal digits. If negative, number of significant digits</param>
		/// <returns>Rounded value</returns>
		/// <example>
		/// - SigFloor(PI, 2) => 3.14
		/// - SigFloor(123456789, 3) => 123,456,000
		/// </example>
		[Pure]
		public static double SigFloor(double x, int digits)
		{
			if (digits > 0)
			{
				double pow = Math.Pow(10, digits);
				return Math.Floor(x * pow) / pow;
			}
			if (digits < 0)
			{
				double pow = Math.Pow(10, -digits);
				return Math.Floor(x / pow) * pow;
			}
			return Math.Floor(x);
		}

		/// <summary>Rounds a value up to the specified number of digits</summary>
		/// <param name="x">Value to round down</param>
		/// <param name="digits">If positive, number of decimal digits. If negative, number of significant digits</param>
		/// <returns>Rounded value</returns>
		/// <example>
		/// - SigCeiling(PI, 2) => 3.15
		/// - SigCeiling(123456789, -3) => 123,457,000
		/// </example>
		[Pure]
		public static double SigCeiling(double x, int digits)
		{
			if (digits > 0)
			{
				double pow = Math.Pow(10, digits);
				return Math.Ceiling(x * pow) / pow;
			}
			if (digits < 0)
			{
				double pow = Math.Pow(10, -digits);
				return Math.Ceiling(x / pow) * pow;
			}
			return Math.Ceiling(x);
		}

		/// <summary>Formats a value</summary>
		/// <param name="x">Value to format</param>
		/// <param name="digits">If positive, number of decimal digits. If negative, number of significant digits</param>
		/// <example>
		/// - SigFormat(PI, 2) => "3.15"
		/// - SigFormat(123456789, -3) => "123,456,000"
		/// </example>
		[Pure]
		public static string SigFormat(double x, int digits)
		{
			if (digits > 0)
			{
				return x.ToString("N" + digits, CultureInfo.InvariantCulture);
			}
			if (digits < 0)
			{
				double pow = Math.Pow(10, -digits);
				return (Math.Ceiling(x / pow) * pow).ToString("N0", CultureInfo.InvariantCulture);
			}
			return x.ToString("N0", CultureInfo.InvariantCulture);
		}

		/// <summary>Renders a Plot</summary>
		/// <param name="ys">Values for the Y coordinates of the points</param>
		/// <param name="width">Width (in characters) of the plot. Final width will be larger to accomodate for the axis and labels</param>
		/// <param name="height">Height (in characters) of the plot. Final eight will be larger to accomodate for the axis and labels</param>
		/// <param name="min">Minimum value on the Y axis (automatically computed if <c>null</c>)</param>
		/// <param name="max">Maximum value on the Y axis (automatically computed if <c>null</c>)</param>
		/// <param name="digits">Number of digits used for rounding. If positive, number of decimal digits (ex: 2 for 1.2345=>'1.23'). If positive, number of significant digits (ex: -3 for 123456=>'123000').</param>
		/// <param name="c">Character used for drawing the points of the series</param>
		/// <returns>Multi-line string that contains the rendered plot</returns>
		[Pure]
		public static string Render(Data ys, int width, int height, double? min = null, double? max = null, [Positive] int digits = 0, char c = '#')
		{
			return RenderFrame(ys, width, height, min, max, digits, c).ToString();
		}

		/// <summary>Renders a Plot</summary>
		/// <param name="series">List of series to plot</param>
		/// <param name="width">Width (in characters) of the plot. Final width will be larger to accomodate for the axis and labels</param>
		/// <param name="height">Height (in characters) of the plot. Final eight will be larger to accomodate for the axis and labels</param>
		/// <param name="min">Minimum value on the Y axis (automatically computed if <c>null</c>)</param>
		/// <param name="max">Maximum value on the Y axis (automatically computed if <c>null</c>)</param>
		/// <param name="digits">Number of digits used for rounding. If positive, number of decimal digits (ex: 2 for 1.2345=>'1.23'). If positive, number of significant digits (ex: -3 for 123456=>'123000').</param>
		/// <param name="chars">String with all the characters used for drawing the points of each series. <paramref name="chars"/>[0] is used to draw <paramref name="series"/>[0], and so on. Modulo is used if there is more series than "colors"</param>
		/// <returns>Multi-line string that contains the rendered plot</returns>
		[Pure]
		public static string Render(Data[] series, int width, int height, double? min = null, double? max = null, int digits = 0, string? chars = null)
		{
			return RenderFrame(series, width, height, min, max, digits, chars).ToString();
		}

		/// <inheritdoc cref="Render(SnowBank.Numerics.RobustChart.Data,int,int,double?,double?,int,char)"/>
		/// <remarks>Rendered frame</remarks>
		[Pure]
		public static FrameBuffer RenderFrame(Data ys, int width, int height, double? min = null, double? max = null, [Positive] int digits = 0, char c = '#')
		{
			var yMin = min ?? ys.Min().GetValueOrDefault();
			var yMax = max ?? ys.Max().GetValueOrDefault();
			return RenderFrame([ ys ], width, height, yMin, yMax, digits, new string(c, 1));
		}

		/// <inheritdoc cref="Render(SnowBank.Numerics.RobustChart.Data[],int,int,double?,double?,int,string?)"/>
		/// <returns>Rendered frame</returns>
		[Pure]
		public static FrameBuffer RenderFrame(Data[] series, int width, int height, double? min = null, double? max = null, int digits = 0, string? chars = null)
		{
			var yMin = SigFloor(min ?? series.Min(s => s.Min()) ?? 0, digits);
			var yMax = SigCeiling(max ?? series.Max(s => s.Max()) ?? 0, digits);
			int count = series[0].Count;

			int d = Math.Max(0, digits);
			int xMargin = 1 + Math.Max(SigFormat(yMin, d).Length, SigFormat(yMax, d).Length);

			var frame = new FrameBuffer(width + xMargin, height);
			var yAxis = Axis.Linear(yMin, yMax, height);
			var xAxis = Axis.Linear(0, count - 1, width);

			frame.DrawXAxis(width, xMargin, end: '+');
			frame.DrawYAxis(height, xMargin, end: '+');
			frame.DrawXAxis(width, xMargin, height - 1, end: '+');
			frame.DrawYAxis(height, xMargin + width - 1, end: '+');

			if (width >= 10)
			{
				frame.DrawXGridLine(width, xMargin, 1 * height / 4, end: '+');
				frame.DrawXGridLine(width, xMargin, 3 * height / 4, end: '+');
			}
			if (height >= 10)
			{
				frame.DrawYGridLine(height, xMargin + (1 * width / 4), end: '+');
				frame.DrawYGridLine(height, xMargin + (3 * width / 4), end: '+');
				frame.DrawText(0, 1 * height / 4, SigFormat(yMin + 1 * (yMax - yMin) / 4, d), xMargin - 1);
				frame.DrawText(0, 3 * height / 4, SigFormat(yMin + 3 * (yMax - yMin) / 4, d), xMargin - 1);
			}
			frame.DrawXGridLine(width, xMargin, height / 2, end: '+');
			frame.DrawYGridLine(height, xMargin + (width / 2), end: '+');

			frame.DrawText(0, 0, SigFormat(yMin, d), xMargin - 1);
			frame.DrawText(0, height / 2, SigFormat((yMin + yMax) / 2, d), xMargin - 1);
			frame.DrawText(0, height - 1, SigFormat(yMax, d), xMargin - 1);

			chars ??= "#*%$ï¿½";
			for (int i = 0; i < series.Length; i++)
			{
				DrawData(frame, series[i], xAxis, yAxis, x0: xMargin, y0: 0, c: chars[i % chars.Length], nc: 'x');
			}
			return frame;
		}

		/// <summary>Renders a XY Plot</summary>
		/// <param name="xs">Series for the X coordinates</param>
		/// <param name="ys">Series for the Y coordinates</param>
		/// <param name="width">Width (in characters) of the plot. Final width will be larger to accomodate for the axis and labels</param>
		/// <param name="height">Height (in characters) of the plot. Final eight will be larger to accomodate for the axis and labels</param>
		/// <param name="xMin">Minimum value of the X axis (automatically computed if <c>null</c>)</param>
		/// <param name="xMax">Maximum value of the X axis (automatically computed if <c>null</c>)</param>
		/// <param name="yMin">Minimum value of the Y axis (automatically computed if <c>null</c>)</param>
		/// <param name="yMax">Maximum value of the Y axis (automatically computed if <c>null</c>)</param>
		/// <param name="digits">Number of digits used for rounding. If positive, number of decimal digits (ex: 2 for 1.2345=>'1.23'). If positive, number of significant digits (ex: -3 for 123456=>'123000').</param>
		/// <param name="c">Character used for drawing the points of the series</param>
		/// <returns>Rendered frame</returns>
		[Pure]
		public static FrameBuffer RenderFrame(Data xs, Data ys, int width, int height, double? xMin = null, double? xMax = null, double? yMin = null, double? yMax = null, int digits = 0, char c = '#')
		{
			var xMinActual = SigFloor(xMin ?? xs.Min() ?? 0, digits);
			var xMaxActual = SigCeiling(xMax ?? xs.Max() ?? 0, digits);
			var yMinActual = SigFloor(yMin ?? ys.Min() ?? 0, digits);
			var yMaxActual = SigCeiling(yMax ?? ys.Max() ?? 0, digits);

			int d = Math.Max(0, digits);
			int xMargin = 1 + Math.Max(SigFormat(yMinActual, d).Length, SigFormat(yMaxActual, d).Length);

			var frame = new FrameBuffer(width + xMargin, height);
			var yAxis = Axis.Linear(yMinActual, yMaxActual, height);
			var xAxis = Axis.Linear(xMinActual, xMaxActual, width);

			frame.DrawXAxis(width, xMargin, end: '+');
			frame.DrawYAxis(height, xMargin, end: '+');
			frame.DrawXAxis(width, xMargin, height - 1, end: '+');
			frame.DrawYAxis(height, xMargin + width - 1, end: '+');

			if (width >= 10)
			{
				frame.DrawXGridLine(width, xMargin, 1 * height / 4, end: '+');
				frame.DrawXGridLine(width, xMargin, 3 * height / 4, end: '+');
			}
			if (height >= 10)
			{
				frame.DrawYGridLine(height, xMargin + (1 * width / 4), end: '+');
				frame.DrawYGridLine(height, xMargin + (3 * width / 4), end: '+');
				frame.DrawText(0, 1 * height / 4, SigFormat(yMinActual + 1 * (yMaxActual - yMinActual) / 4, d), xMargin - 1);
				frame.DrawText(0, 3 * height / 4, SigFormat(yMinActual + 3 * (yMaxActual - yMinActual) / 4, d), xMargin - 1);
			}
			frame.DrawXGridLine(width, xMargin, height / 2, end: '+');
			frame.DrawYGridLine(height, xMargin + (width / 2), end: '+');

			frame.DrawText(0, 0, SigFormat(yMinActual, d), xMargin - 1);
			frame.DrawText(0, height / 2, SigFormat((yMinActual + yMaxActual) / 2, d), xMargin - 1);
			frame.DrawText(0, height - 1, SigFormat(yMaxActual, d), xMargin - 1);

			DrawData(frame, xs, ys, xAxis, yAxis, x0: xMargin, y0: 0, c: c, nc: 'x');
			return frame;
		}

	}

}
