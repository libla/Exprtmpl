parser grammar ExprtmplParser;

options {
	tokenVocab = ExprtmplLexer;
}

content:
	.*? (expr .*?)* EOF
	;
expr:
	EXPRSTART value RBRACE
	;
control:
	(include|end|forloop1|forloop2|forrange|if|elseif|else) EOF
	;
include:
	INCLUDE (WITH value)? NEWLINE?
	;
end:
	K_END NEWLINE?
	;
forloop1:
	K_FOR NAME K_IN value NEWLINE?
	;
forloop2:
	K_FOR NAME COMMA NAME K_IN value NEWLINE?
	;
forrange:
	K_FOR NAME ASSIGN numeric COMMA numeric (COMMA numeric)? NEWLINE?
	;
if:
	K_IF or NEWLINE?
	;
elseif:
	K_ELSEIF or NEWLINE?
	;
else:
	K_ELSE NEWLINE?
	;
value:
	member|K_NULL|concat|or|numeric|array|table|call
	;
member:
	NAME suffix*
	;
suffix:
	DOT NAME|LBRACK index RBRACK|LBRACK subindex COLON subindex RBRACK
	;
index:
	concat|numeric
	;
subindex:
	numeric
	;
concat:
	string (CONCAT string)*
	;
string:
	(STRING|member|LPART concat RPART) (LBRACK substring (COLON substring)? RBRACK)?
	;
substring:
	numeric
	;
or:
	and (OR and)*
	;
and:
	boolean (AND boolean)*
	;
boolean:
	NOT? (K_TRUE|K_FALSE|member (EQ K_NULL)?|numeric (CMP|EQ) numeric|concat EQ concat|LPART or RPART)
	;
numeric:
	mulexp (ADD mulexp)*
	;
mulexp:
	powexp (MUL powexp)*
	;
powexp:
	atom (POW atom)*
	;
atom:
	HEX|NEGATE? NUMBER|NEGATE? member|NEGATE? LPART numeric RPART
	;
array:
	LBRACK value? (COMMA value)* COMMA? RBRACK
	;
table:
	LBRACE (NAME COLON value)? (COMMA NAME COLON value)* COMMA? RBRACE
	;
call:
	NAME (DOT NAME)* LPART value? (COMMA value)* RPART
	;