using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Grammophone.Formulae.Evaluation
{
  /// <summary>
  /// Exception thrown during formula evaluation.
  /// </summary>
  [Serializable]
  public class FormulaEvaluationException : Exception
  {
    /// <inheritdoc/>
    public FormulaEvaluationException(string message) : base(message) { }

		/// <inheritdoc/>
		public FormulaEvaluationException(string message, Exception inner) : base(message, inner) { }

		/// <inheritdoc/>
		protected FormulaEvaluationException(
      System.Runtime.Serialization.SerializationInfo info,
      System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
  }
}
