using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using One.Transforms.InferTypesPlugins.Helpers;
using One.Ast;
using One;

namespace One.Transforms.InferTypesPlugins
{
    public class ReturnTypeInferer {
        public bool returnsNull = false;
        public bool throws = false;
        public List<Type_> returnTypes;
        public ErrorManager errorMan;
        
        public ReturnTypeInferer(ErrorManager errorMan)
        {
            this.errorMan = errorMan;
            this.returnTypes = new List<Type_>();
        }
        
        public void addReturn(Expression returnValue) {
            if (returnValue is NullLiteral) {
                this.returnsNull = true;
                return;
            }
            
            var returnType = returnValue.actualType;
            if (returnType == null)
                throw new Error("Return type cannot be null");
            
            if (!this.returnTypes.some((Type_ x) => { return Type_.equals(x, returnType); }))
                this.returnTypes.push(returnType);
        }
        
        public Type_ finish(Type_ declaredType, string errorContext, ClassType asyncType) {
            Type_ inferredType = null;
            
            if (this.returnTypes.length() == 0) {
                if (this.throws)
                    inferredType = declaredType ?? VoidType.instance;
                else if (this.returnsNull) {
                    if (declaredType != null)
                        inferredType = declaredType;
                    else
                        this.errorMan.throw_($"{errorContext} returns only null and it has no declared return type!");
                }
                else
                    inferredType = VoidType.instance;
            }
            else if (this.returnTypes.length() == 1)
                inferredType = this.returnTypes.get(0);
            else if (declaredType != null && this.returnTypes.every((Type_ x, int i) => { return Type_.isAssignableTo(x, declaredType); }))
                inferredType = declaredType;
            else {
                this.errorMan.throw_($"{errorContext} returns different types: {this.returnTypes.map((Type_ x) => { return x.repr(); }).join(", ")}");
                inferredType = AnyType.instance;
            }
            
            var checkType = declaredType;
            if (checkType != null && asyncType != null && checkType is ClassType && ((ClassType)checkType).decl == asyncType.decl)
                checkType = ((ClassType)checkType).typeArguments.get(0);
            
            if (checkType != null && !Type_.isAssignableTo(inferredType, checkType))
                this.errorMan.throw_($"{errorContext} returns different type ({inferredType.repr()}) than expected {checkType.repr()}");
            
            this.returnTypes = null;
            return declaredType != null ? declaredType : inferredType;
        }
    }
    
    public class InferReturnType : InferTypesPlugin {
        public List<ReturnTypeInferer> returnTypeInfer;
        
        public ReturnTypeInferer current {
            get {
            
                return this.returnTypeInfer.get(this.returnTypeInfer.length() - 1);
            }
        }
        
        public InferReturnType(): base("InferReturnType")
        {
            this.returnTypeInfer = new List<ReturnTypeInferer>();
        }
        
        public void start() {
            this.returnTypeInfer.push(new ReturnTypeInferer(this.errorMan));
        }
        
        public Type_ finish(Type_ declaredType, string errorContext, ClassType asyncType) {
            return this.returnTypeInfer.pop().finish(declaredType, errorContext, asyncType);
        }
        
        public override bool handleStatement(Statement stmt) {
            if (stmt is ReturnStatement && ((ReturnStatement)stmt).expression != null) {
                this.main.processStatement(((ReturnStatement)stmt));
                if (this.returnTypeInfer.length() != 0)
                    this.current.addReturn(((ReturnStatement)stmt).expression);
                return true;
            }
            else if (stmt is ThrowStatement) {
                if (this.returnTypeInfer.length() != 0)
                    this.current.throws = true;
                return false;
            }
            else
                return false;
        }
        
        public override bool handleLambda(Lambda lambda) {
            this.start();
            this.main.processLambda(lambda);
            lambda.returns = this.finish(lambda.returns, "Lambda", null);
            lambda.setActualType(new LambdaType(lambda.parameters, lambda.returns), false, true);
            return true;
        }
        
        public override bool handleMethod(IMethodBase method) {
            if (method is Method && ((Method)method).body != null) {
                this.start();
                this.main.processMethodBase(((Method)method));
                ((Method)method).returns = this.finish(((Method)method).returns, $"Method \"{((Method)method).name}\"", ((Method)method).async ? this.main.currentFile.literalTypes.promise : null);
                return true;
            }
            else
                return false;
        }
        
        public override bool handleProperty(Property prop) {
            this.main.processVariable(prop);
            
            if (prop.getter != null) {
                this.start();
                this.main.processBlock(prop.getter);
                prop.type = this.finish(prop.type, $"Property \"{prop.name}\" getter", null);
            }
            
            if (prop.setter != null) {
                this.start();
                this.main.processBlock(prop.setter);
                this.finish(VoidType.instance, $"Property \"{prop.name}\" setter", null);
            }
            
            return true;
        }
    }
}