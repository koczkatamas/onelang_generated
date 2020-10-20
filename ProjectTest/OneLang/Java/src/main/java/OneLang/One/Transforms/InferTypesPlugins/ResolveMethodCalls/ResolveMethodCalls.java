import java.util.Arrays;
import java.util.ArrayList;

public class ResolveMethodCalls extends InferTypesPlugin {
    public ResolveMethodCalls()
    {
        super("ResolveMethodCalls");
        
    }
    
    protected Method findMethod(IInterface cls, String methodName, Boolean isStatic, Expression[] args)
    {
        var allBases = cls instanceof Class ? Arrays.stream(((Class)cls).getAllBaseInterfaces()).filter(x -> x instanceof Class).toArray(IInterface[]::new) : cls.getAllBaseInterfaces();
        
        var methods = new ArrayList<Method>();
        for (var base : allBases)
            for (var m : base.getMethods()) {
                var minLen = Arrays.stream(m.getParameters()).filter(p -> p.getInitializer() == null).toArray(MethodParameter[]::new).length;
                var maxLen = m.getParameters().length;
                var match = m.name == methodName && m.getIsStatic() == isStatic && minLen <= args.length && args.length <= maxLen;
                if (match)
                    methods.add(m);
            }
        
        if (methods.size() == 0)
            throw new Error("Method '" + methodName + "' was not found on type '" + cls.getName() + "' with " + args.length + " arguments");
        else if (methods.size() > 1) {
            // TODO: actually we should implement proper method shadowing here...
            var thisMethods = methods.stream().filter(x -> x.parentInterface == cls).toArray(Method[]::new);
            if (thisMethods.length == 1)
                return thisMethods[0];
            throw new Error("Multiple methods found with name '" + methodName + "' and " + args.length + " arguments on type '" + cls.getName() + "'");
        }
        return methods.get(0);
    }
    
    protected void resolveReturnType(IMethodCallExpression expr, GenericsResolver genericsResolver)
    {
        genericsResolver.collectFromMethodCall(expr);
        
        for (Integer i = 0; i < expr.getArgs().length; i++) {
            // actually doesn't have to resolve, but must check if generic type confirm the previous argument with the same generic type
            var paramType = genericsResolver.resolveType(expr.getMethod().getParameters()[i].getType(), false);
            if (paramType != null)
                expr.getArgs()[i].setExpectedType(paramType);
            expr.getArgs()[i] = this.main.runPluginsOn(expr.getArgs()[i]);
            genericsResolver.collectResolutionsFromActualType(paramType, expr.getArgs()[i].actualType);
        }
        
        if (expr.getMethod().returns == null) {
            this.errorMan.throw_("Method (" + expr.getMethod().parentInterface.getName() + "::" + expr.getMethod().name + ") return type was not specified or infered before the call.");
            return;
        }
        
        expr.setActualType(genericsResolver.resolveType(expr.getMethod().returns, true), true, expr instanceof InstanceMethodCallExpression && TypeHelper.isGeneric(((InstanceMethodCallExpression)expr).object.getType()));
    }
    
    protected Expression transformMethodCall(UnresolvedMethodCallExpression expr)
    {
        if (expr.object instanceof ClassReference || expr.object instanceof StaticThisReference) {
            var cls = expr.object instanceof ClassReference ? ((ClassReference)expr.object).decl : expr.object instanceof StaticThisReference ? ((StaticThisReference)expr.object).cls : null;
            var method = this.findMethod(cls, expr.methodName, true, expr.args);
            var result = new StaticMethodCallExpression(method, expr.typeArgs, expr.args, expr.object instanceof StaticThisReference);
            this.resolveReturnType(result, new GenericsResolver());
            return result;
        }
        else {
            var resolvedObject = expr.object.actualType != null ? expr.object : this.main.runPluginsOn(expr.object) != null ? this.main.runPluginsOn(expr.object) : expr.object;
            var objectType = resolvedObject.getType();
            var intfType = objectType instanceof ClassType ? ((IInterface)((ClassType)objectType).decl) : objectType instanceof InterfaceType ? ((InterfaceType)objectType).decl : null;
            
            if (intfType != null) {
                var method = this.findMethod(intfType, expr.methodName, false, expr.args);
                var result = new InstanceMethodCallExpression(resolvedObject, method, expr.typeArgs, expr.args);
                this.resolveReturnType(result, GenericsResolver.fromObject(resolvedObject));
                return result;
            }
            else if (objectType instanceof AnyType) {
                expr.setActualType(AnyType.instance);
                return expr;
            }
            else { }
            return resolvedObject;
        }
    }
    
    public Boolean canTransform(Expression expr)
    {
        return expr instanceof UnresolvedMethodCallExpression && !(((UnresolvedMethodCallExpression)expr).actualType instanceof AnyType);
    }
    
    public Expression transform(Expression expr)
    {
        return this.transformMethodCall(((UnresolvedMethodCallExpression)expr));
    }
}