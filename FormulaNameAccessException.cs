using System;
using System.Collections.Generic;
using System.Text;

namespace Grammophone.Formulae.Evaluation
{
  /// <summary>
  /// Exception thrown when a furmula attempts to access a namespace or member name in the specified set of excluded names.
  /// </summary>
  [Serializable]
  public class FormulaNameAccessException : FormulaEvaluationException
  {
		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="message">The error message.</param>
		/// <param name="name">The namespace or member name that was attempted to be accessed.</param>
		public FormulaNameAccessException(string message, string name) : base(message)
    {
      this.Name = name;
    }

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="message">The error message.</param>
		/// <param name="name">The namespace or member name that was attempted to be accessed.</param>
		/// <param name="inner">The inner exception.</param>
		public FormulaNameAccessException(string message, string name, Exception inner) : base(message, inner)
		{
			this.Name = name;
		}

		/// <inheritdoc/>
		protected FormulaNameAccessException(
      System.Runtime.Serialization.SerializationInfo info,
      System.Runtime.Serialization.StreamingContext context) : base(info, context) 
    {
      if (this.Name == null) this.Name = String.Empty;
    }

    /// <summary>
    /// The namespace or member name that was attempted to be accessed.
    /// </summary>
    public string Name { get; }
  }
}
