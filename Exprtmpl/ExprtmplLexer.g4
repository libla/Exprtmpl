lexer grammar ExprtmplLexer;

NEWLINE:
	('\r\n'|'\n'|'\r'|'\f')+
	;
BLANK:
	(' '|'\t'|'\u000B')+ -> skip
	;
CMP:
	'<='|'>='|'<'|'>'
	;
EQ:
	'=='|'!='
	;
ADD:
	'+'|'-'
	;
MUL:
	'*'|'/'|'%'
	;
NOT:
	'!'
	;
NEGATE:
	'-'
	;
INCLUDE:
	K_INCLUDE BLANK ('a'..'z'|'A'..'Z'|'_'|'-'|'0'..'9')+('.' ('a'..'z'|'A'..'Z'|'_'|'-'|'0'..'9')+)*
	;
K_INCLUDE:
	'import'
	;
K_END:
	'end'
	;
K_FOR:
	'for'
	;
K_IN:
	'in'
	;
K_IF:
	'if'
	;
K_ELSEIF:
	'elseif'
	;
K_ELSE:
	'else'
	;
K_TRUE:
	'true'
	;
K_FALSE:
	'false'
	;
K_NULL:
	'null'
	;
NAME:
	('a'..'z'|'A'..'Z'|'_')('a'..'z'|'A'..'Z'|'_'|'0'..'9')*
	;
HEX:
	'0'('X'|'x')('a'..'f'|'A'..'F'|'0'..'9')+
	;
NUMBER:
	(DECIMAL|INTEGER) EXP?
	;
STRING:
	'"' (ESCAPE|~('\\'|'"'))* '"'|'\'' (ESCAPE|~('\\'|'\''))* '\''
	;
LPART:
	'('
	;
RPART:
	')'
	;
LBRACK:
	'['
	;
RBRACK:
	']'
	;
LBRACE:
	'{' -> pushMode(DEFAULT_MODE)
	;
RBRACE:
	'}' -> popMode
	;
DOT:
	'.'
	;
POW:
	'^'
	;
AND:
	'&'
	;
OR:
	'|'
	;
COLON:
	':'
	;
COMMA:
	','
	;
CONCAT:
	'..'
	;
ASSIGN:
	'='
	;
WITH:
	'with'
	;

fragment INTEGER:
	'0'|('-')? ('1'..'9') ('0'..'9')*
	;
fragment DECIMAL:
	(INTEGER|('-''0'))'.'('0'..'9')+
	;
fragment EXP:
	('e'|'E')('+'|'-')?('0'..'9')+
	;
fragment ESCAPE:
	'\\'('t'|'r'|'n'|'f'|'\\'|'"'|'\'')|UNICODE
	;
fragment UNICODE:
	'\\''u'('a'..'f'|'A'..'F'|'0'..'9')('a'..'f'|'A'..'F'|'0'..'9')('a'..'f'|'A'..'F'|'0'..'9')('a'..'f'|'A'..'F'|'0'..'9')
	;

mode Content;

EXPRSTART:
	'={' -> pushMode(DEFAULT_MODE)
	;
OTHER:
	. -> skip
	;