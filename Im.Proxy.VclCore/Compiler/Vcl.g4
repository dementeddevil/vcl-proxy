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
	:	'.' backendVariableExpression
	;

backendVariableExpression
	:	backendStringVariableExpression  ';'
	|	backendIntegerVariableExpression ';'
	|	backendTimeVariableExpression ';'
	|	backendProbeVariableExpression
	;

backendStringVariableExpression
	:	name=backendStringVariableName '=' value=stringLiteral
	;

backendIntegerVariableExpression
	:	name=backendIntegerVariableName '=' value=integerLiteral
	;

backendTimeVariableExpression
	:	name=backendTimeVariableName '=' value=timeLiteral
	;

backendProbeVariableExpression
	:	name='probe' '=' value=probeExpression
	;

backendStringVariableName
	:	'host'
	|	'port'
	|	'host_header'
	|	'proxy_header'
	;

backendIntegerVariableName
	:	'max_connections'
	;

backendTimeVariableName
	:	'connect_timeout'
	|	'first_byte_timeout'
	|	'between_bytes_timeout'
	;

probeExpression
	:	probeReferenceExpression
	|	probeInlineExpression
	;

probeReferenceExpression
	:	probeName=Identifier ';'
	;

probeDeclaration
	:	'probe' Identifier probeInlineExpression
	;

probeInlineExpression
	:	'{' probeElementList '}'
	;

probeElementList
	:	probeElement
	|	probeElementList probeElement
	;

probeElement
	:	'.' probeVariableExpression ';'
	;

probeVariableExpression
	:	probeStringVariableExpression
	|	probeIntegerVariableExpression
	|	probeTimeVariableExpression
	;

probeStringVariableExpression
	:	name=probeStringVariableName '=' value=stringLiteral
	;

probeIntegerVariableExpression
	:	name=probeIntegerVariableName '=' value=integerLiteral
	;

probeTimeVariableExpression
	:	name=probeTimeVariableName '=' value=timeLiteral
	;

probeStringVariableName
	:	'url'
	;

probeTimeVariableName
	:	'timeout'
	|	'interval'
	;

probeIntegerVariableName
	:	'expected_response'
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

stringLiteral
	:	StringConstant
	;

integerLiteral
	:	IntegerConstant
	;

timeLiteral
	:	TimeConstant
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
	:	Digit+
	;

TimeConstant
	:	Digit+ ('ms' | 's' | 'm' | 'h' | 'd' | 'w' | 'y')
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

fragment
IdentifierHypen
	:	IdentifierNonHyphen
	|	Hyphen IdentifierNonHyphen
	;

fragment
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

fragment
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
