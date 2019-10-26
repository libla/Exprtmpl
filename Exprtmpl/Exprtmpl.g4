grammar Exprtmpl;

control:
	(include|end|forloop1|forloop2|forrange|if|elseif|else) EOF
	;
content:
	.*? (expr .*?)* EOF
	;
include:
	INCLUDE ('with' value)? NEWLINE?
	;
end:
	K_END NEWLINE?
	;
forloop1:
	K_FOR NAME K_IN value NEWLINE?
	;
forloop2:
	K_FOR NAME ',' NAME K_IN value NEWLINE?
	;
forrange:
	K_FOR NAME '=' numeric ',' numeric (',' numeric)? NEWLINE?
	;
if:
	K_IF (member|or) NEWLINE?
	;
elseif:
	K_ELSEIF (member|or) NEWLINE?
	;
else:
	K_ELSE NEWLINE?
	;
expr:
	':{' value '}'
	;
value:
	member|K_NULL|concat|or|numeric|array|table|call
	;
member:
	NAME suffix*
	;
suffix:
	'.' NAME|'[' index ']'|'[' subindex ':' subindex ']'
	;
index:
	member|concat|numeric
	;
subindex:
	member|numeric
	;
concat:
	string ('..' string)*
	;
string:
	(STRING|member|'(' concat ')') ('[' substring (':' substring)? ']')?
	;
substring:
	member|numeric
	;
or:
	and ('|' and)*
	;
and:
	boolean ('&' boolean)*
	;
boolean:
	NOT? (K_TRUE|K_FALSE|member|numeric (CMP|EQ) numeric|concat EQ concat|'(' or ')')
	;
numeric:
	mulexp (ADD mulexp)*
	;
mulexp:
	powexp (MUL powexp)*
	;
powexp:
	atom ('^' atom)*
	;
atom:
	HEX|NEGATE? NUMBER|NEGATE? member|NEGATE? '(' numeric ')'
	;
array:
	'[' value? (',' value)* ','? ']'
	;
table:
	'{' (NAME ':' value)? (',' NAME ':' value)* ','? '}'
	;
call:
	NAME '(' value? (',' value)* ')'
	;
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