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
	|	functionDeclaration
	;

includeDeclaration
	:	'include' StringLiteral ';'
	;

backendDeclaration
	:	'backend' Identifier '{' backendElementList '}'
	;

backendElementList
	:	backendElement
	|	backendElementList backendElement
	;

backendElement
	:	'.' Identifier '=' constantExpression ';'
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
	|	returnStatement
	|	compoundStatement
	;

expressionStatement
	:	expression? ';'
	;

ifStatement
	:	'if' '(' expression ')' statement ('else' statement)?
	;

setStatement
	:	'set' dottedExpression '=' expression ';'
	;

removeStatement
	:	('remove' | 'unset') Identifier ';'
	;

errorStatement
	:	'error' HttpStatusCode StringLiteral ';'
	;

syntheticStatement
	:	'synthetic' syntheticExpression ';'
	;

returnStatement
	:	'return' '(' returnStateExpression ')' ';'
	;

returnStateExpression
	:	simpleReturnStatePrimitives
	|	complexReturnStatePrimitives
	;
	
simpleReturnStatePrimitives
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

complexReturnStatePrimitives
	:	returnSynthStateExpression
	;

returnSynthStateExpression
	:	'synth' '(' statusCode=HttpStatusCode (',' statusDescription=StringLiteral )? ')'
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
	:	Constant
	|	StringLiteral
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
	|	equalityExpression '==' castExpression
	|	equalityExpression '!=' castExpression
	|	equalityExpression '~' matchExpression
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
	:	StringLiteral
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

Constant
	:	IntegerConstant
	|	StringConstant
	|	TimeConstant
	;

fragment
IntegerConstant
	:	DecimalConstant
	;

fragment
TimeConstant
	:	DecimalConstant ('s' | 'm' | 'h')
	;

fragment
DecimalConstant
	:	Digit+
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


StringLiteral
	:	StringConstant
	;

fragment
StringConstant
	:	'"' CharacterSequence? '"'
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
