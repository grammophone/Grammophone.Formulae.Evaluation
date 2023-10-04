﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Grammophone.Formulae.Evaluation
{
	/// <summary>
	/// Severity level of a <see cref="FormulaDiagnostic"/>.
	/// </summary>
	public enum FormulaDiagnosticSeverity
	{
		/// <summary>
		/// Something that is an issue, as determined by some authority,
		/// but is not surfaced through normal means.
		/// There may be different mechanisms that act on these issues.
		/// </summary>
		Hidden = 0,

		/// <summary>
		/// Information that does not indicate a problem (i.e. not prescriptive).
		/// </summary>
		Info = 1,

		/// <summary>
		/// Something suspicious but allowed.
		/// </summary>
		Warning = 2,

		/// <summary>
		/// Something not allowed by the rules of the language or other authority.
		/// </summary>
		Error = 3
	}
}
