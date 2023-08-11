using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HookGenExtender.Utilities.Representations {

	/// <summary>
	/// A utility class that wraps around <see cref="IMemberDef"/> such that it compares equality based on
	/// the name and parameter types. This assumes the member is a method or property.
	/// <para/>
	/// This is used in favor of a <see cref="IEqualityComparer{T}"/> so that 
	/// this type can be used in <see cref="HashSet{T}"/>.
	/// </summary>
	public sealed class SignatureBoundMemberDef {

		public IMemberDef Original { get; }

		public SignatureBoundMemberDef(IMemberDef original) {
			if (original is MethodDef || original is PropertyDef) {
				Original = original;
			} else {
				throw new NotSupportedException("This type only supports methods and properties.");
			}
		}

		/// <summary>
		/// For use in predicates, this is a proxy to the constructor.
		/// </summary>
		/// <param name="from"></param>
		/// <returns></returns>
		public static SignatureBoundMemberDef ctor(IMemberDef from) => new SignatureBoundMemberDef(from);

		public override string ToString() => Original.ToString();

		public override int GetHashCode() {
			unchecked {
				int nameHash = Original.Name.GetHashCode();
				int paramHash = 0;
				if (Original is MethodDef method) {
					//paramHash = method.MethodSig.GetHashCode();
					method.SelectParameters(null, out TypeSig[] inputParameters, out TypeSig returnParameter, out _, false);
					foreach (TypeSig param in inputParameters) {
						paramHash ^= param.FullName.GetHashCode();
					}
					paramHash ^= returnParameter.FullName.GetHashCode();

				} else if (Original is PropertyDef prop) {
					paramHash = prop.PropertySig.RetType.FullName.GetHashCode();
				}
				return paramHash ^ nameHash;
			}
		}

		public override bool Equals(object obj) {
			if (ReferenceEquals(obj, this)) return true;
			if (obj is SignatureBoundMemberDef other) {
				if (ReferenceEquals(other.Original, Original)) return true;
				if (Original.Equals(other.Original)) return true;
				if (Original.Name != other.Original.Name) return false;
				if (Original is MethodDef thisMethod && other.Original is MethodDef otherMethod) {
					//if (thisMethod.MethodSig != otherMethod.MethodSig) return false;
					
					if (thisMethod.Parameters.Count != otherMethod.Parameters.Count) return false;
					thisMethod.SelectParameters(null, out TypeSig[] thisParameters, out TypeSig thisReturnParameter, out _, false);
					otherMethod.SelectParameters(null, out TypeSig[] otherParameters, out TypeSig otherReturnParameter, out _, false);
					if (thisReturnParameter.FullName != otherReturnParameter.FullName) return false;
					for (int i = 0; i < thisParameters.Length; i++) {
						if (thisParameters[i].FullName != otherParameters[i].FullName) return false;
					}
					
					return true;
				} else if (Original is PropertyDef thisProperty && other.Original is PropertyDef otherProperty) {
					//if (thisProperty.PropertySig.RetType.FullName != otherProperty.PropertySig.RetType.FullName) return false;
					if (thisProperty.PropertySig != otherProperty.PropertySig) return false;
					return true;
				}
			}
			return false;
		}


	}
}
