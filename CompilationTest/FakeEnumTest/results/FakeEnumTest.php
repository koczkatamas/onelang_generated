<?php

class TokenType {
    public static $end_token = "EndToken";
    public static $whitespace = "Whitespace";
    public static $identifier = "Identifier";
    public static $operator_x = "Operator";
    public static $no_initializer;
}

$casing_test = TokenType::$end_token;
print($casing_test . "\n");