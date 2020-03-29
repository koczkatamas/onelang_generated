using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using One.Transforms.InferTypesPlugins.Helpers;
using One.Ast;

namespace One.Transforms.InferTypesPlugins
{
    public class ResolveEnumMemberAccess : InferTypesPlugin {
        public ResolveEnumMemberAccess(): base("ResolveEnumMemberAccess")
        {
            
        }
        
        public override bool canTransform(Expression expr) {
            return expr is PropertyAccessExpression && ((PropertyAccessExpression)expr).object_ is EnumReference;
        }
        
        public override Expression transform(Expression expr) {
            var pa = ((PropertyAccessExpression)expr);
            var enumMemberRef = ((EnumReference)pa.object_);
            var member = enumMemberRef.decl.values.find((EnumMember x) => { return x.name == pa.propertyName; });
            if (member == null) {
                this.errorMan.throw_($"Enum member was not found: {enumMemberRef.decl.name}::{pa.propertyName}");
                return null;
            }
            return new EnumMemberReference(member);
        }
        
        public override bool canDetectType(Expression expr) {
            return expr is EnumMemberReference;
        }
        
        public override bool detectType(Expression expr) {
            expr.setActualType((((EnumMemberReference)expr)).decl.parentEnum.type);
            return true;
        }
    }
}