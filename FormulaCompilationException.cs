using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Grammophone.Formulae.Evaluation
{
  /// <summary>
  /// Exception thrown when the formulae cannot be compiled by a <see cref="FormulaEvaluator{C}"/>.
  /// </summary>
  [Serializable]
  public class FormulaCompilationException : FormulaEvaluationException
  {
		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="message">The message.</param>
		/// <param name="diagnostics">The diagnostics of the compilation.</param>
		public FormulaCompilationException(string message, IEnumerable<FormulaDiagnostic> diagnostics) : base(message)
    {
      this.Diagnostics = diagnostics;
    }
    
    /// <summary>
    /// Create.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="diagnostics">The diagnostics of the compilation.</param>
    /// <param name="inner">The inner exception.</param>
    public FormulaCompilationException(string message, IEnumerable<FormulaDiagnostic> diagnostics, Exception inner) : base(message, inner)
    {
      this.Diagnostics = diagnostics;
    }
    
    /// <inheritdoc/>
    protected FormulaCompilationException(
      System.Runtime.Serialization.SerializationInfo info,
      System.Runtime.Serialization.StreamingContext context) : base(info, context) 
    {
      if (this.Diagnostics == null) this.Diagnostics = Enumerable.Empty<FormulaDiagnostic>();
    }

		/// <summary>
		/// The diagnostics of the compilation.
		/// </summary>
		public IEnumerable<FormulaDiagnostic> Diagnostics { get; }
  }
}
