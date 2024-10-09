using System;
using System.Collections.Generic;
using System.Text;

namespace Grammophone.Formulae.Evaluation
{
	/// <summary>
	/// Options for rounding decimal variables.
	/// </summary>
	[Serializable]
	public class RoundingOptions
	{
		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="roundedDecimalsCount">The number of decimal digits to round.</param>
		/// <param name="midpointRounding">The strategy of rounding to use if the result is in the middle of rounded values.</param>
		public RoundingOptions(int roundedDecimalsCount, MidpointRounding midpointRounding)
		{
			this.RoundedDecimalsCount = roundedDecimalsCount;
			this.MidpointRounding = midpointRounding;
		}

		/// <summary>
		/// The number of decimal digits to round.
		/// </summary>
		public int RoundedDecimalsCount { get; }

		/// <summary>
		/// The strategy of rounding to use if the result is in the middle of rounded values.
		/// </summary>
		public MidpointRounding MidpointRounding { get; }
	}
}
