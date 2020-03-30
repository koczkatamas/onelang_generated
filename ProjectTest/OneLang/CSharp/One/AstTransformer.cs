using System.Linq;
using One.Ast;
using One;

namespace One
{
    public class AstTransformer {
        public ErrorManager errorMan;
        public SourceFile currentFile;
        public IInterface currentInterface;
        public IMethodBase currentMethod;
        public Statement currentStatement;
        public string name;
        
        public AstTransformer(string name)
        {
            this.name = name;
            this.errorMan = new ErrorManager();
            this.currentFile = null;
            this.currentInterface = null;
            this.currentMethod = null;
            this.currentStatement = null;
        }
        
        protected virtual Type_ visitType(Type_ type) {
            if (type is ClassType classType || type is InterfaceType || type is UnresolvedType) {
                var type2 = ((IHasTypeArguments)type);
                type2.typeArguments = type2.typeArguments.map(x => this.visitType(x) ?? x);
            }
            else if (type is LambdaType lambdType) {
                foreach (var mp in lambdType.parameters)
                    this.visitMethodParameter(mp);
                lambdType.returnType = this.visitType(lambdType.returnType) ?? lambdType.returnType;
            }
            return null;
        }
        
        protected virtual Expression visitIdentifier(Identifier id) {
            return null;
        }
        
        protected virtual IVariable visitVariable(IVariable variable) {
            if (variable.type != null)
                variable.type = this.visitType(variable.type) ?? variable.type;
            return null;
        }
        
        protected virtual IVariableWithInitializer visitVariableWithInitializer(IVariableWithInitializer variable) {
            this.visitVariable(variable);
            if (variable.initializer != null)
                variable.initializer = this.visitExpression(variable.initializer) ?? variable.initializer;
            return null;
        }
        
        protected virtual VariableDeclaration visitVariableDeclaration(VariableDeclaration stmt) {
            this.visitVariableWithInitializer(stmt);
            return null;
        }
        
        protected Statement visitUnknownStatement(Statement stmt) {
            this.errorMan.throw_($"Unknown statement type");
            return null;
        }
        
        protected virtual Statement visitStatement(Statement stmt) {
            this.currentStatement = stmt;
            if (stmt is ReturnStatement retStat) {
                if (retStat.expression != null)
                    retStat.expression = this.visitExpression(retStat.expression) ?? retStat.expression;
            }
            else if (stmt is ExpressionStatement exprStat)
                exprStat.expression = this.visitExpression(exprStat.expression) ?? exprStat.expression;
            else if (stmt is IfStatement ifStat) {
                ifStat.condition = this.visitExpression(ifStat.condition) ?? ifStat.condition;
                ifStat.then = this.visitBlock(ifStat.then) ?? ifStat.then;
                if (ifStat.else_ != null)
                    ifStat.else_ = this.visitBlock(ifStat.else_) ?? ifStat.else_;
            }
            else if (stmt is ThrowStatement throwStat)
                throwStat.expression = this.visitExpression(throwStat.expression) ?? throwStat.expression;
            else if (stmt is VariableDeclaration varDecl)
                return this.visitVariableDeclaration(varDecl);
            else if (stmt is WhileStatement whileStat) {
                whileStat.condition = this.visitExpression(whileStat.condition) ?? whileStat.condition;
                whileStat.body = this.visitBlock(whileStat.body) ?? whileStat.body;
            }
            else if (stmt is DoStatement doStat) {
                doStat.condition = this.visitExpression(doStat.condition) ?? doStat.condition;
                doStat.body = this.visitBlock(doStat.body) ?? doStat.body;
            }
            else if (stmt is ForStatement forStat) {
                if (forStat.itemVar != null)
                    this.visitVariableWithInitializer(forStat.itemVar);
                forStat.condition = this.visitExpression(forStat.condition) ?? forStat.condition;
                forStat.incrementor = this.visitExpression(forStat.incrementor) ?? forStat.incrementor;
                forStat.body = this.visitBlock(forStat.body) ?? forStat.body;
            }
            else if (stmt is ForeachStatement forStat2) {
                this.visitVariable(forStat2.itemVar);
                forStat2.items = this.visitExpression(forStat2.items) ?? forStat2.items;
                forStat2.body = this.visitBlock(forStat2.body) ?? forStat2.body;
            }
            else if (stmt is TryStatement tryStat) {
                tryStat.tryBody = this.visitBlock(tryStat.tryBody) ?? tryStat.tryBody;
                if (tryStat.catchBody != null) {
                    this.visitVariable(tryStat.catchVar);
                    tryStat.catchBody = this.visitBlock(tryStat.catchBody) ?? tryStat.catchBody;
                }
                if (tryStat.finallyBody != null)
                    tryStat.finallyBody = this.visitBlock(tryStat.finallyBody) ?? tryStat.finallyBody;
            }
            else if (stmt is BreakStatement) { }
            else if (stmt is UnsetStatement unsetStat)
                unsetStat.expression = this.visitExpression(unsetStat.expression) ?? unsetStat.expression;
            else if (stmt is ContinueStatement) { }
            else
                return this.visitUnknownStatement(stmt);
            return null;
        }
        
        protected virtual Block visitBlock(Block block) {
            block.statements = block.statements.map(x => this.visitStatement(x) ?? x).ToList();
            return null;
        }
        
        protected TemplateString visitTemplateString(TemplateString expr) {
            for (int i = 0; i < expr.parts.length(); i++) {
                var part = expr.parts.get(i);
                if (!part.isLiteral)
                    part.expression = this.visitExpression(part.expression) ?? part.expression;
            }
            return null;
        }
        
        protected Expression visitUnknownExpression(Expression expr) {
            this.errorMan.throw_($"Unknown expression type");
            return null;
        }
        
        protected virtual Lambda visitLambda(Lambda lambda) {
            this.visitMethodBase(lambda);
            return null;
        }
        
        protected virtual VariableReference visitVariableReference(VariableReference varRef) {
            return null;
        }
        
        protected virtual Expression visitExpression(Expression expr) {
            if (expr is BinaryExpression binExpr) {
                binExpr.left = this.visitExpression(binExpr.left) ?? binExpr.left;
                binExpr.right = this.visitExpression(binExpr.right) ?? binExpr.right;
            }
            else if (expr is NullCoalesceExpression nullCoalExpr) {
                nullCoalExpr.defaultExpr = this.visitExpression(nullCoalExpr.defaultExpr) ?? nullCoalExpr.defaultExpr;
                nullCoalExpr.exprIfNull = this.visitExpression(nullCoalExpr.exprIfNull) ?? nullCoalExpr.exprIfNull;
            }
            else if (expr is UnresolvedCallExpression unrCallExpr) {
                unrCallExpr.func = this.visitExpression(unrCallExpr.func) ?? unrCallExpr.func;
                unrCallExpr.typeArgs = unrCallExpr.typeArgs.map(x => this.visitType(x) ?? x);
                unrCallExpr.args = unrCallExpr.args.map(x => this.visitExpression(x) ?? x);
            }
            else if (expr is UnresolvedMethodCallExpression unrMethCallExpr) {
                unrMethCallExpr.object_ = this.visitExpression(unrMethCallExpr.object_) ?? unrMethCallExpr.object_;
                unrMethCallExpr.typeArgs = unrMethCallExpr.typeArgs.map(x => this.visitType(x) ?? x);
                unrMethCallExpr.args = unrMethCallExpr.args.map(x => this.visitExpression(x) ?? x);
            }
            else if (expr is ConditionalExpression condExpr) {
                condExpr.condition = this.visitExpression(condExpr.condition) ?? condExpr.condition;
                condExpr.whenTrue = this.visitExpression(condExpr.whenTrue) ?? condExpr.whenTrue;
                condExpr.whenFalse = this.visitExpression(condExpr.whenFalse) ?? condExpr.whenFalse;
            }
            else if (expr is Identifier ident)
                return this.visitIdentifier(ident);
            else if (expr is UnresolvedNewExpression unrNewExpr) {
                this.visitType(unrNewExpr.cls);
                unrNewExpr.args = unrNewExpr.args.map(x => this.visitExpression(x) ?? x);
            }
            else if (expr is NewExpression newExpr) {
                this.visitType(newExpr.cls);
                newExpr.args = newExpr.args.map(x => this.visitExpression(x) ?? x);
            }
            else if (expr is TemplateString templStr)
                return this.visitTemplateString(templStr);
            else if (expr is ParenthesizedExpression parExpr)
                parExpr.expression = this.visitExpression(parExpr.expression) ?? parExpr.expression;
            else if (expr is UnaryExpression unaryExpr)
                unaryExpr.operand = this.visitExpression(unaryExpr.operand) ?? unaryExpr.operand;
            else if (expr is PropertyAccessExpression propAccExpr)
                propAccExpr.object_ = this.visitExpression(propAccExpr.object_) ?? propAccExpr.object_;
            else if (expr is ElementAccessExpression elemAccExpr) {
                elemAccExpr.object_ = this.visitExpression(elemAccExpr.object_) ?? elemAccExpr.object_;
                elemAccExpr.elementExpr = this.visitExpression(elemAccExpr.elementExpr) ?? elemAccExpr.elementExpr;
            }
            else if (expr is ArrayLiteral arrayLit)
                arrayLit.items = arrayLit.items.map(x => this.visitExpression(x) ?? x);
            else if (expr is MapLiteral mapLit)
                foreach (var item in mapLit.items)
                    item.value = this.visitExpression(item.value) ?? item.value;
            else if (expr is StringLiteral) { }
            else if (expr is BooleanLiteral) { }
            else if (expr is NumericLiteral) { }
            else if (expr is NullLiteral) { }
            else if (expr is RegexLiteral) { }
            else if (expr is CastExpression castExpr) {
                castExpr.newType = this.visitType(castExpr.newType) ?? castExpr.newType;
                castExpr.expression = this.visitExpression(castExpr.expression) ?? castExpr.expression;
            }
            else if (expr is InstanceOfExpression instOfExpr) {
                instOfExpr.expr = this.visitExpression(instOfExpr.expr) ?? instOfExpr.expr;
                instOfExpr.checkType = this.visitType(instOfExpr.checkType) ?? instOfExpr.checkType;
            }
            else if (expr is AwaitExpression awaitExpr)
                awaitExpr.expr = this.visitExpression(awaitExpr.expr) ?? awaitExpr.expr;
            else if (expr is Lambda lambd)
                return this.visitLambda(lambd);
            else if (expr is ClassReference) { }
            else if (expr is EnumReference) { }
            else if (expr is ThisReference) { }
            else if (expr is StaticThisReference) { }
            else if (expr is MethodParameterReference methParRef)
                return this.visitVariableReference(methParRef);
            else if (expr is VariableDeclarationReference varDeclRef)
                return this.visitVariableReference(varDeclRef);
            else if (expr is ForVariableReference forVarRef)
                return this.visitVariableReference(forVarRef);
            else if (expr is ForeachVariableReference forVarRef2)
                return this.visitVariableReference(forVarRef2);
            else if (expr is CatchVariableReference catchVarRef)
                return this.visitVariableReference(catchVarRef);
            else if (expr is GlobalFunctionReference) { }
            else if (expr is SuperReference) { }
            else if (expr is InstanceFieldReference instFieldRef)
                return this.visitVariableReference(instFieldRef);
            else if (expr is InstancePropertyReference instPropRef)
                return this.visitVariableReference(instPropRef);
            else if (expr is StaticFieldReference statFieldRef)
                return this.visitVariableReference(statFieldRef);
            else if (expr is StaticPropertyReference statPropRef)
                return this.visitVariableReference(statPropRef);
            else if (expr is EnumMemberReference) { }
            else if (expr is StaticMethodCallExpression statMethCallExpr) {
                statMethCallExpr.typeArgs = statMethCallExpr.typeArgs.map(x => this.visitType(x) ?? x);
                statMethCallExpr.args = statMethCallExpr.args.map(x => this.visitExpression(x) ?? x);
            }
            else if (expr is GlobalFunctionCallExpression globFunctCallExpr)
                globFunctCallExpr.args = globFunctCallExpr.args.map(x => this.visitExpression(x) ?? x);
            else if (expr is InstanceMethodCallExpression instMethCallExpr) {
                instMethCallExpr.object_ = this.visitExpression(instMethCallExpr.object_) ?? instMethCallExpr.object_;
                instMethCallExpr.typeArgs = instMethCallExpr.typeArgs.map(x => this.visitType(x) ?? x);
                instMethCallExpr.args = instMethCallExpr.args.map(x => this.visitExpression(x) ?? x);
            }
            else
                return this.visitUnknownExpression(expr);
            return null;
        }
        
        protected void visitMethodParameter(MethodParameter methodParameter) {
            this.visitVariableWithInitializer(methodParameter);
        }
        
        protected virtual void visitMethodBase(IMethodBase method) {
            foreach (var item in method.parameters)
                this.visitMethodParameter(item);
            
            if (method.body != null)
                method.body = this.visitBlock(method.body) ?? method.body;
        }
        
        protected void visitMethod(Method method) {
            this.currentMethod = method;
            this.visitMethodBase(method);
            method.returns = this.visitType(method.returns) ?? method.returns;
            this.currentMethod = null;
        }
        
        protected void visitGlobalFunction(GlobalFunction func) {
            this.visitMethodBase(func);
            func.returns = this.visitType(func.returns) ?? func.returns;
        }
        
        protected void visitConstructor(Constructor constructor) {
            this.currentMethod = constructor;
            this.visitMethodBase(constructor);
            this.currentMethod = null;
        }
        
        protected virtual void visitField(Field field) {
            this.visitVariableWithInitializer(field);
        }
        
        protected virtual void visitProperty(Property prop) {
            this.visitVariable(prop);
            if (prop.getter != null)
                prop.getter = this.visitBlock(prop.getter) ?? prop.getter;
            if (prop.setter != null)
                prop.setter = this.visitBlock(prop.setter) ?? prop.setter;
        }
        
        protected virtual void visitInterface(Interface intf) {
            this.currentInterface = intf;
            intf.baseInterfaces = intf.baseInterfaces.map(x => this.visitType(x) ?? x);
            foreach (var field in intf.fields)
                this.visitField(field);
            foreach (var method in intf.methods)
                this.visitMethod(method);
            this.currentInterface = null;
        }
        
        protected virtual void visitClass(Class cls) {
            this.currentInterface = cls;
            if (cls.constructor_ != null)
                this.visitConstructor(cls.constructor_);
            
            cls.baseClass = this.visitType(cls.baseClass) ?? cls.baseClass;
            cls.baseInterfaces = cls.baseInterfaces.map(x => this.visitType(x) ?? x);
            foreach (var field in cls.fields)
                this.visitField(field);
            foreach (var prop in cls.properties)
                this.visitProperty(prop);
            foreach (var method in cls.methods)
                this.visitMethod(method);
            this.currentInterface = null;
        }
        
        protected virtual void visitEnum(Enum_ enum_) {
            foreach (var value in enum_.values)
                this.visitEnumMember(value);
        }
        
        protected void visitEnumMember(EnumMember enumMember) {
            
        }
        
        public virtual void visitSourceFile(SourceFile sourceFile) {
            this.errorMan.resetContext(this);
            this.currentFile = sourceFile;
            foreach (var enum_ in sourceFile.enums)
                this.visitEnum(enum_);
            foreach (var intf in sourceFile.interfaces)
                this.visitInterface(intf);
            foreach (var cls in sourceFile.classes)
                this.visitClass(cls);
            foreach (var func in sourceFile.funcs)
                this.visitGlobalFunction(func);
            sourceFile.mainBlock = this.visitBlock(sourceFile.mainBlock) ?? sourceFile.mainBlock;
            this.currentFile = null;
        }
        
        public virtual void visitPackage(Package pkg) {
            foreach (var file in Object.values(pkg.files))
                this.visitSourceFile(file);
        }
    }
}