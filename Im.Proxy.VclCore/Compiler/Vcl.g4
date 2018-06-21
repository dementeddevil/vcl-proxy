grammar Vcl;

/*
 * Parser Rules
 */

compileUnit
	:	translationUnit? EOF
	;

translationUnit
	:	declaration
	|	translationUnit declaration
	;

declaration
	:	includeDeclaration
	|	backendDeclaration
	|	probeDeclaration
	|	functionDeclaration
	;

includeDeclaration
	:	'include' StringConstant ';'
	;

backendDeclaration
	:	'backend' Identifier '{' backendElementList '}'
	;

backendElementList
	:	backendElement
	|	backendElementList backendElement
	;

backendElement
	:	'.' key='host' '=' StringConstant ';'
	|	'.' key='port' '=' ('http' | 'https') ';'
	|	'.' key='host_header' '=' constantExpression ';'
	|	'.' key='connect_timeout' '=' TimeConstant ';'
	|	'.' key='first_byte_timeout' '=' TimeConstant ';'
	|	'.' key='between_bytes_timeout' '=' TimeConstant ';'
	|	'.' key='proxy_header' '=' StringConstant ';'
	|	'.' key='max_connections' '=' IntegerConstant ';'
	|	'.' key='probe' '=' probeExpression ';'
	;

probeExpression
	:	probeReferenceExpression
	|	probeInlineExpression
	;

probeReferenceExpression
	:	probeName=Identifier ';'
	;

probeInlineExpression
	:	'{' probeElementList '}'
	;

probeDeclaration
	:	'probe' Identifier '{' probeElementList '}'
	;

probeElementList
	:	probeElement
	|	probeElementList probeElement
	;

probeElement
	:	'.' probeVariableName '=' constantExpression ';'
	;

probeVariableName
	:	'url'
	|	'request'
	|	'expected_response'
	|	'timeout'
	|	'interval'
	|	'initial'
	|	'window'
	|	'threshold'
	;

aclDeclaration
	:	'acl' Identifier '{' aclElementList '}'
	;

aclElementList
	:	ignoreableAclElement
	|	aclElementList ignoreableAclElement
	;

ignoreableAclElement
	:	aclElement
	|	'(' aclElement ')'
	;

aclElement
	:	('!')? ipAddressOrHost
	|	subnet=SubnetMask
	;

ipAddressOrHost
	:	StringConstant
	|	IpAddress
	;

functionDeclaration
	:	'sub' Identifier compoundStatement
	;

statement
	:	expressionStatement
	|	ifStatement
	|	setStatement
	|	removeStatement
	|	errorStatement
	|	syntheticStatement
	|	callStatement
	|	returnStatement
	|	compoundStatement
	;

expressionStatement
	:	expression? ';'
	;

ifStatement
	:	'if' '(' expression ')' statement (('elif' | 'else if') '(' expression ')' statement)? ('else' statement)?
	;

setStatement
	:	'set' dottedExpression '=' expression ';'
	;

removeStatement
	:	('remove' | 'unset') Identifier ';'
	;

errorStatement
	:	'error' HttpStatusCode StringConstant ';'
	;

syntheticStatement
	:	'synthetic' syntheticExpression ';'
	;

callStatement
	:	'call' subroutineName = Identifier ';'
	;

returnStatement
	:	'return' '(' returnStateExpression ')' ';'
	;

returnStateExpression
	:	simpleReturnStateExpression
	|	complexReturnStateExpression
	;
	
simpleReturnStateExpression
	:	'restart'
	|	'receive'
	|	'hash'
	|	'lookup'
	|	'busy'
	|	'purge'
	|	'pass'
	|	'pipe'
	|	'hit'
	|	'miss'
	|	'hit-for-pass'
	|	'fetch'
	|	'deliver'
	|	'done'
	|	'abandon'
	|	'retry'
	|	'error'
	;

complexReturnStateExpression
	:	returnSynthStateExpression
	;

returnSynthStateExpression
	:	'synth' '(' statusCode=HttpStatusCode (',' statusDescription=StringConstant )? ')'
	;

compoundStatement
	:	'{' blockItemList? '}'
	;

blockItemList
	:	blockItem
	|	blockItemList blockItem
	;

blockItem
	:	statement
	;

expression
	:	assignmentExpression
	;

assignmentExpression
	:	conditionalExpression
	|	unaryExpression assignmentOperator assignmentExpression
	;

assignmentOperator
	:	'='
	;

conditionalExpression
	:	logicalOrExpression
	;

primaryExpression
	:	dottedExpression
	|	constantExpression
	|	'(' expression ')'
	;

dottedExpression
	:	Identifier
	|	dottedExpression '.' Identifier
	;

constantExpression
	:	StringConstant
	|	IntegerConstant
	|	TimeConstant
	|	DecimalConstant
	|	BooleanConstant
	;

unaryExpression
	:	primaryExpression
	|	unaryOperator castExpression
	;

unaryOperator
	:	'!'
	;

castExpression
	:	unaryExpression
	;

equalityExpression
	:	castExpression
	|	equalityExpression op=equalityOperator castExpression
	;

equalityOperator
	:	'=='
	|	'!='
	|	'~'
	|	'!~'
	;

logicalAndExpression
	:	equalityExpression
	|	logicalAndExpression '&&' equalityExpression
	;

logicalOrExpression
	:	logicalAndExpression
	|	logicalOrExpression '||' logicalAndExpression
	;

matchExpression
	:	regularExpression
	|	backendReferenceExpression
	;

regularExpression
	:	StringConstant
	;

backendReferenceExpression
	:	Identifier
	;

syntheticExpression
	:	syntheticSubExpression
	|	syntheticExpression '+' syntheticSubExpression
	;

syntheticSubExpression
	:	SyntheticString
	|	primaryExpression
	;
/*
 * Lexer Rules
 */

HttpStatusCode
	:	Digit Digit Digit
	;

Identifier
    :   IdentifierNondigit IdentifierHypen*
    ;

IntegerConstant
	:	DecimalConstant
	;

TimeConstant
	:	DecimalConstant ('ms' | 's' | 'm' | 'h' | 'd' | 'w' | 'y')
	;

DecimalConstant
	:	Digit+
	;

BooleanConstant
	:	'false'
	|	'true'
	;

StringConstant
	:	'"' CharacterSequence? '"'
	;

fragment
IdentifierNondigit
    :   Nondigit
    ;

IdentifierHypen
	:	IdentifierNonHyphen
	|	Hyphen IdentifierNonHyphen
	;

IdentifierNonHyphen
	:	IdentifierNondigit
    |   Digit
	;

DigitSequence
    :   Digit+
    ;

HexEncoding
	:	('%' [a-fA-F0-9] [a-fA-F0-9]) +
	;

SubnetMask
	:	IpAddressSequence '/' Digit+
	;

IpAddress
	:	IpAddressSequence
	;

fragment
IpAddressSequence
	:	'"' Digit+ '.' Digit+ '.' Digit+ '.' Digit+ '"'
	;

fragment
CharacterSequence
	:	Char+
	;

fragment
Char
	:	~["\\\r\n]
	|	HexEncoding
	;

fragment
Nondigit
    :   [a-zA-Z_]
    ;

fragment
Digit
    :   [0-9]
    ;

Hyphen
	:	'-'
	;

Whitespace
    :   [ \t]+
        -> skip
    ;

Newline
    :   (   '\r' '\n'?
        |   '\n'
        )
        -> skip
    ;

SyntheticString
	:	'{"' .*? '"}'
	;

BlockComment
    :   '/*' .*? '*/'
        -> skip
    ;

LineComment
    :   ('# ' | '//') ~[\r\n]*
        -> skip
    ;
