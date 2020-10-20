import java.util.List;
import java.util.regex.Pattern;
import java.util.ArrayList;
import java.util.stream.Collectors;
import java.util.Arrays;

public class Reader {
    public Integer wsOffset = 0;
    public Integer offset = 0;
    public CursorPositionSearch cursorSearch;
    public String lineComment = "//";
    public Boolean supportsBlockComment = true;
    public String blockCommentStart = "/*";
    public String blockCommentEnd = "*/";
    public Boolean commentDisabled = false;
    public String identifierRegex = "[A-Za-z_][A-Za-z0-9_]*";
    public String numberRegex = "[+-]?(\\d*\\.\\d+|\\d+\\.\\d+|0x[0-9a-fA-F_]+|0b[01_]+|[0-9_]+)";
    public List<ParseError> errors;
    public IReaderHooks hooks;
    public Integer wsLineCounter = 0;
    public Boolean moveWsOffset = true;
    public Integer prevTokenOffset;
    public String input;
    
    public Boolean getEof() {
        return this.offset >= this.input.length();
    }
    
    public Cursor getCursor() {
        return this.cursorSearch.getCursorForOffset(this.offset);
    }
    
    public String getPreview() {
        var preview = this.input.substring(this.offset, this.offset + 30).replaceAll(Pattern.quote("\\n"), "\\n");
        if (preview.length() == 30)
            preview += "...";
        return preview;
    }
    
    public Reader(String input)
    {
        this.input = input;
        this.errors = new ArrayList<ParseError>();
        this.hooks = null;
        this.prevTokenOffset = -1;
        this.cursorSearch = new CursorPositionSearch(input);
    }
    
    public String linePreview(Cursor cursor)
    {
        var line = this.input.substring(cursor.lineStart, cursor.lineEnd - 1);
        return line + "\n" + " ".repeat(cursor.column - 1) + "^^^";
    }
    
    public void fail(String message, Integer offset)
    {
        var error = new ParseError(message, this.cursorSearch.getCursorForOffset(offset == -1 ? this.offset : offset), this);
        this.errors.add(error);
        
        if (this.hooks != null)
            this.hooks.errorCallback(error);
        else
            throw new Error(message + " at " + error.cursor.line + ":" + error.cursor.column + "\n" + this.linePreview(error.cursor));
    }
    
    public void fail(String message) {
        this.fail(message, -1);
    }
    
    public void skipWhitespace(Boolean includeInTrivia)
    {
        for (; this.offset < this.input.length(); this.offset++) {
            var c = this.input.substring(this.offset, this.offset + 1);
            
            if (c == "\n")
                this.wsLineCounter++;
            
            if (!(c == "\n" || c == "\r" || c == "\t" || c == " "))
                break;
        }
        
        if (!includeInTrivia)
            this.wsOffset = this.offset;
    }
    
    public void skipWhitespace() {
        this.skipWhitespace(false);
    }
    
    public Boolean skipUntil(String token)
    {
        var index = this.input.indexOf(token, this.offset);
        if (index == -1)
            return false;
        this.offset = index + token.length();
        if (this.moveWsOffset)
            this.wsOffset = this.offset;
        return true;
    }
    
    public void skipLine()
    {
        if (!this.skipUntil("\n"))
            this.offset = this.input.length();
    }
    
    public Boolean isAlphaNum(String c)
    {
        var n = (int)c.charAt(0);
        return (97 <= n && n <= 122) || (65 <= n && n <= 90) || (48 <= n && n <= 57) || n == 95;
    }
    
    public Boolean readExactly(String what)
    {
        if (this.input.startsWith(what, this.offset)) {
            this.wsOffset = this.offset = this.offset + what.length();
            return true;
        }
        return false;
    }
    
    public String readChar()
    {
        // TODO: should we move wsOffset?
        this.offset++;
        return this.input.substring(this.offset - 1, this.offset - 1 + 1);
    }
    
    public Boolean peekToken(String token)
    {
        this.skipWhitespaceAndComment();
        
        if (this.input.startsWith(token, this.offset)) {
            // TODO: hackish way to make sure space comes after word tokens
            if (this.isAlphaNum(token.substring(token.length() - 1, token.length() - 1 + 1)) && this.isAlphaNum(this.input.substring(this.offset + token.length(), this.offset + token.length() + 1)))
                return false;
            return true;
        }
        else
            return false;
    }
    
    public Boolean readToken(String token)
    {
        if (this.peekToken(token)) {
            this.prevTokenOffset = this.offset;
            this.wsOffset = this.offset = this.offset + token.length();
            return true;
        }
        return false;
    }
    
    public String readAnyOf(String[] tokens)
    {
        for (var token : tokens) {
            if (this.readToken(token))
                return token;
        }
        return null;
    }
    
    public void expectToken(String token, String errorMsg)
    {
        if (!this.readToken(token))
            this.fail(errorMsg != null ? errorMsg : "expected token '" + token + "'");
    }
    
    public void expectToken(String token) {
        this.expectToken(token, null);
    }
    
    public String expectString(String errorMsg)
    {
        var result = this.readString();
        if (result == null)
            this.fail(errorMsg != null ? errorMsg : "expected string");
        return result;
    }
    
    public String expectString() {
        return this.expectString(null);
    }
    
    public String expectOneOf(String[] tokens)
    {
        var result = this.readAnyOf(tokens);
        if (result == null)
            this.fail("expected one of the following tokens: " + Arrays.stream(tokens).collect(Collectors.joining(", ")));
        return result;
    }
    
    public static String[] matchFromIndex(String pattern, String input, Integer offset)
    {
        var regex = new RegExp(pattern, "gy");
        regex.lastIndex = offset;
        var matches = regex.exec(input);
        if (matches == null)
            return null;
        else {
            var result = new ArrayList<String>();
            for (Integer i = 0; i < matches.length; i++)
                result.add(matches[i]);
            return result.toArray(String[]::new);
        }
    }
    
    public String[] peekRegex(String pattern)
    {
        var matches = Reader.matchFromIndex(pattern, this.input, this.offset);
        return matches;
    }
    
    public String[] readRegex(String pattern)
    {
        var matches = Reader.matchFromIndex(pattern, this.input, this.offset);
        if (matches != null) {
            this.prevTokenOffset = this.offset;
            this.wsOffset = this.offset = this.offset + matches[0].length();
        }
        return matches;
    }
    
    public void skipWhitespaceAndComment()
    {
        if (this.commentDisabled)
            return;
        
        this.moveWsOffset = false;
        while (true) {
            this.skipWhitespace(true);
            if (this.input.startsWith(this.lineComment, this.offset))
                this.skipLine();
            else if (this.supportsBlockComment && this.input.startsWith(this.blockCommentStart, this.offset)) {
                if (!this.skipUntil(this.blockCommentEnd))
                    this.fail("block comment end (\"" + this.blockCommentEnd + "\") was not found");
            }
            else
                break;
        }
        this.moveWsOffset = true;
    }
    
    public String readLeadingTrivia()
    {
        this.skipWhitespaceAndComment();
        var thisLineStart = this.input.lastIndexOf("\n", this.offset);
        if (thisLineStart <= this.wsOffset)
            return "";
        
        var result = this.input.substring(this.wsOffset, thisLineStart + 1);
        result = Utils.deindent(result);
        this.wsOffset = thisLineStart;
        return result;
    }
    
    public String readIdentifier()
    {
        this.skipWhitespace();
        var idMatch = this.readRegex(this.identifierRegex);
        if (idMatch == null)
            return null;
        
        return idMatch[0];
    }
    
    public String readNumber()
    {
        this.skipWhitespace();
        var numMatch = this.readRegex(this.numberRegex);
        if (numMatch == null)
            return null;
        
        if (this.readRegex("[0-9a-zA-Z]") != null)
            this.fail("invalid character in number");
        
        return numMatch[0];
    }
    
    public String readString()
    {
        this.skipWhitespace();
        
        var sepChar = this.input.substring(this.offset, this.offset + 1);
        if (sepChar != "'" && sepChar != "\"")
            return null;
        
        var str = "";
        this.readExactly(sepChar);
        while (!this.readExactly(sepChar)) {
            var chr = this.readChar();
            if (chr == "\\") {
                var esc = this.readChar();
                if (esc == "n")
                    str += "\n";
                else if (esc == "r")
                    str += "\r";
                else if (esc == "t")
                    str += "\t";
                else if (esc == "\\")
                    str += "\\";
                else if (esc == sepChar)
                    str += sepChar;
                else
                    this.fail("invalid escape", this.offset - 1);
            }
            else {
                var chrCode = (int)chr.charAt(0);
                if (!(32 <= chrCode && chrCode <= 126) || chr == "\\" || chr == sepChar)
                    this.fail("not allowed character (code=" + chrCode + ")", this.offset - 1);
                str += chr;
            }
        }
        return str;
    }
    
    public String expectIdentifier(String errorMsg)
    {
        var id = this.readIdentifier();
        if (id == null)
            this.fail(errorMsg != null ? errorMsg : "expected identifier");
        return id;
    }
    
    public String expectIdentifier() {
        return this.expectIdentifier(null);
    }
    
    public String[] readModifiers(String[] modifiers)
    {
        var result = new ArrayList<String>();
        while (true) {
            var success = false;
            for (var modifier : modifiers) {
                if (this.readToken(modifier)) {
                    result.add(modifier);
                    success = true;
                }
            }
            if (!success)
                break;
        }
        return result.toArray(String[]::new);
    }
}