import java.util.Map;
import java.util.HashMap;
import java.util.Arrays;
import java.util.stream.Collectors;

public class PackageStateCapture {
    public Map<String, String> overviews;
    public Package pkg;
    
    public PackageStateCapture(Package pkg)
    {
        this.pkg = pkg;
        this.overviews = new HashMap<String, String>();
        for (var file : pkg.files.values().toArray(SourceFile[]::new))
            this.overviews.put(file.sourcePath.path, new TSOverviewGenerator(false, true).generate(file));
    }
    
    public String getSummary()
    {
        return Arrays.stream(Arrays.stream(this.overviews.keySet().toArray(String[]::new)).map(file -> "=== " + file + " ===\n\n" + this.overviews.get(file)).toArray(String[]::new)).collect(Collectors.joining("\n\n"));
    }
}