using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace HookGenExtender.Utilities {
	public sealed class ImportCache : IDisposable {

		private readonly Dictionary<Type, ITypeDefOrRef> _typeImports = new Dictionary<Type, ITypeDefOrRef>();
		private readonly Dictionary<Type, TypeSig> _typeSigByTypeImports = new Dictionary<Type, TypeSig>();
		private readonly Dictionary<FieldInfo, MemberRef> _fieldImports = new Dictionary<FieldInfo, MemberRef>();
		private readonly Dictionary<MethodBase, IMethod> _methodImports = new Dictionary<MethodBase, IMethod>();
		private readonly Dictionary<IType, IType> _typeItfImports = new Dictionary<IType, IType>();
		private readonly Dictionary<TypeDef, TypeRef> _typeDefImports = new Dictionary<TypeDef, TypeRef>();
		private readonly Dictionary<TypeRef, TypeRef> _typeRefImports = new Dictionary<TypeRef, TypeRef>();
		private readonly Dictionary<TypeSpec, TypeSpec> _typeSpecImports = new Dictionary<TypeSpec, TypeSpec>();
		private readonly Dictionary<TypeSig, TypeSig> _typeSigImports = new Dictionary<TypeSig, TypeSig>();
		private readonly Dictionary<IField, MemberRef> _fieldItfImports = new Dictionary<IField, MemberRef>();
		private readonly Dictionary<FieldDef, MemberRef> _fieldDefImports = new Dictionary<FieldDef, MemberRef>();
		private readonly Dictionary<IMethod, IMethod> _mtdItfImports = new Dictionary<IMethod, IMethod>();
		private readonly Dictionary<MethodDef, MemberRef> _mtdDefImports = new Dictionary<MethodDef, MemberRef>();
		private readonly Dictionary<MethodSpec, MethodSpec> _mtdSpecImports = new Dictionary<MethodSpec, MethodSpec>();
		private readonly Dictionary<MemberRef, MemberRef> _mbrRefImports = new Dictionary<MemberRef, MemberRef>();

		private readonly ModuleDef _module;

		public ImportCache(ModuleDef module) {
			_module = module;
		}

		public ITypeDefOrRef Import(Type type) => _typeImports.GetOrCreate(type, _module.Import);
		public TypeSig ImportAsTypeSig(Type type) => _typeSigByTypeImports.GetOrCreate(type, _module.ImportAsTypeSig);
		public MemberRef Import(FieldInfo type) => _fieldImports.GetOrCreate(type, _module.Import);
		public IMethod Import(MethodBase type) => _methodImports.GetOrCreate(type, _module.Import);
		public IType Import(IType type) => _typeItfImports.GetOrCreate(type, _module.Import);
		public TypeRef Import(TypeDef type) => _typeDefImports.GetOrCreate(type, _module.Import);
		public TypeRef Import(TypeRef type) => _typeRefImports.GetOrCreate(type, _module.Import);
		public TypeSpec Import(TypeSpec type) => _typeSpecImports.GetOrCreate(type, _module.Import);
		public TypeSig Import(TypeSig type) => _typeSigImports.GetOrCreate(type, _module.Import);
		public MemberRef Import(IField type) => _fieldItfImports.GetOrCreate(type, _module.Import);
		public MemberRef Import(FieldDef type) => _fieldDefImports.GetOrCreate(type, _module.Import);
		public IMethod Import(IMethod type) => _mtdItfImports.GetOrCreate(type, _module.Import);
		public MemberRef Import(MethodDef type) => _mtdDefImports.GetOrCreate(type, _module.Import);
		public MethodSpec Import(MethodSpec type) => _mtdSpecImports.GetOrCreate(type, _module.Import);
		public MemberRef Import(MemberRef type) => _mbrRefImports.GetOrCreate(type, _module.Import);

		public void Dispose() {
			_typeImports.Clear();
			_typeSigByTypeImports.Clear();
			_fieldImports.Clear();
			_methodImports.Clear();
			_typeItfImports.Clear();
			_typeDefImports.Clear();
			_typeRefImports.Clear();
			_typeSpecImports.Clear();
			_typeSigImports.Clear();
			_fieldItfImports.Clear();
			_fieldDefImports.Clear();
			_mtdItfImports.Clear();
			_mtdDefImports.Clear();
			_mtdSpecImports.Clear();
			_mbrRefImports.Clear();
		}
	}
}
