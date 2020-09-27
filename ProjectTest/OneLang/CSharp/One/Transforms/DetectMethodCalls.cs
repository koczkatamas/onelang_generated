using One;
using One.Ast;

namespace One.Transforms
{
    public class DetectMethodCalls : AstTransformer {
        public DetectMethodCalls(): base("DetectMethodCalls")
        {
            
        }
        
        protected override Expression visitExpression(Expression expr)
        {
            base.visitExpression(expr);
            if (expr is UnresolvedCallExpression unrCallExpr && unrCallExpr.func is PropertyAccessExpression propAccExpr)
                return new UnresolvedMethodCallExpression(propAccExpr.object_, propAccExpr.propertyName, unrCallExpr.typeArgs, unrCallExpr.args);
            return null;
        }
    }
}