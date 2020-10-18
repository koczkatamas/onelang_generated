import java.util.Arrays;

public class ResolveGenericTypeIdentifiers extends AstTransformer {
    public ResolveGenericTypeIdentifiers()
    {
        super("ResolveGenericTypeIdentifiers");
        
    }
    
    protected IType visitType(IType type)
    {
        super.visitType(type);
        
        //console.log(type && type.constructor.name, JSON.stringify(type));
        if (type instanceof UnresolvedType && ((this.currentInterface instanceof Class && Arrays.stream(((Class)this.currentInterface).getTypeArguments()).anyMatch(((UnresolvedType)type).typeName::equals)) || (this.currentMethod instanceof Method && Arrays.stream(((Method)this.currentMethod).typeArguments).anyMatch(((UnresolvedType)type).typeName::equals))))
            return new GenericsType(((UnresolvedType)type).typeName);
        
        return null;
    }
}