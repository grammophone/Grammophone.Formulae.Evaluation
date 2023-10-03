using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Scripting;

namespace Grammophone.Formulae.Evaluation
{
	/// <summary>
	/// The result of an evaluation.
	/// </summary>
	public class EvaluationState
	{
		#region Private fields

		private ImmutableArray<EvaluationVariable> variables;

		private ScriptState scriptState;

		private ImmutableDictionary<string, EvaluationVariable>? variablesByName;

		#endregion

		#region Construction

		internal EvaluationState(ScriptState scriptState)
		{
			if (scriptState == null) throw new ArgumentNullException(nameof(scriptState));

			this.scriptState = scriptState;
		}

		#endregion

		#region Public properties

		/// <summary>
		/// The variables computed in the evaluation.
		/// </summary>
		public IReadOnlyList<EvaluationVariable> Variables
		{
			get
			{
				if (variables.IsDefault)
				{
					ImmutableInterlocked.InterlockedInitialize(ref variables, CreateVariables());
				}

				return variables;
			}
		}

		/// <summary>
		/// The variables computed in the evaluation, indexed by their name.
		/// </summary>
		public IReadOnlyDictionary<string, EvaluationVariable> VariablesByName
		{
			get
			{
				if (variablesByName == null)
				{
					variablesByName = CreateVariablesByName();
				}

				return variablesByName;
			}
		}

		/// <summary>
		/// The return value produced by the evaulation.
		/// </summary>
		public object? ReturnValue
		{
			get
			{
				return scriptState.ReturnValue;
			}
		}

		#endregion

		#region Private methods

		private ImmutableArray<EvaluationVariable> CreateVariables()
		{
			return scriptState.Variables.Select(sv => new EvaluationVariable(sv.Name, sv.Type, sv.IsReadOnly, sv.Value)).ToImmutableArray();
		}

		private ImmutableDictionary<string, EvaluationVariable> CreateVariablesByName()
		{
			return variables.ToImmutableDictionary(ev => ev.Name);
		}

		#endregion
	}
}
