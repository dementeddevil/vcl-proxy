grammar Vcl;

@parser::members{
	public bool InCustomFunction { get; set; }
}

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
	|	procedureDeclaration
	|	aclDeclaration
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
	:	'acl' name=Identifier '{' aclElementList '}'
	;

aclElementList
	:	aclElement
	|	aclElementList aclElement
	;

aclElement
	:	exclude=AclExclude? aclIpAddressOrHost
	;

aclIpAddressOrHost
	:	host=StringConstant				# AclEntryNonIgnorableHost
	|	'(' host=StringConstant ')'		# AclEntryIgnorableHost
	|	address=IpAddress				# AclEntryIpAddress
	|	subnet=SubnetMask				# AclEntrySubnetMask
	;

procedureDeclaration
	:	'sub' name=VclIdentifier compoundStatement		{InCustomFunction = false;}		# SystemProcedureDeclaration
	|	'sub' name=Identifier compoundStatement			{InCustomFunction = true;}		# CustomProcedureDeclaration
	;

statement
	:	varStatement
	|	ifStatement
	|	setStatement
	|	removeStatement
	|	errorStatement
	|	syntheticStatement
	|	callStatement
	|	returnStatement
	|	restartStatement
	|	compoundStatement
	|	expressionStatement
	;

expressionStatement
	:	expression? ';'
	;

varStatement
	:	'declare' 'local' 'var.' name=Identifier type=('BOOL' | 'INTEGER' | 'FLOAT' | 'TIME' | 'RTIME' | 'STRING') ';'
	;

ifStatement
	:	'if' '(' test=conditionalOrExpression ')' ifTrue=statement (('elseif' | 'elsif' | 'elif') '(' otherTest=conditionalOrExpression ')' otherTrue=statement)* ('else' elseStmt=statement)?
	;

setStatement
	:	'set' lhs=memberAccessExpression '=' rhs=expression ';'
	;

removeStatement
	:	('remove' | 'unset') id=memberAccessExpression ';'
	;

errorStatement
	:	'error' statusCode=IntegerConstant statusDescription=StringConstant ';'
	;

syntheticStatement
	:	'synthetic' stringLiteral ';'
	;

callStatement
	:	'call' subroutineName=Identifier ';'
	;

hashDataStatement
	:	'hash_data' '(' expr=expression ')' ';'
	;

returnStatement
	:	{InCustomFunction}? 'return' ';'
	|	'return' '(' returnStateExpression ')' ';'
	;

restartStatement
	:	'restart' ';'
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
	:	'synth' '(' statusCode=IntegerConstant (',' statusDescription=StringConstant )? ')'
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
	|	nonAssignmentExpression
	;

assignmentExpression
	:	lhs=unaryExpression op=assignmentOperator rhs=expression
	;

nonAssignmentExpression
	:	conditionalExpression
	;

assignmentOperator
	:	'=' | '+=' | '-='
	;

conditionalExpression
	:	'if' '(' conditionalOrExpression ',' ifTrue=expression ',' ifFalse=expression ')'
	;

conditionalOrExpression
	:	lhs=conditionalAndExpression ('||' rhs=conditionalAndExpression)*
	;

conditionalAndExpression
	:	lhs=inclusiveOrExpression ('&&' rhs=inclusiveOrExpression)*
	;

inclusiveOrExpression
	:	lhs=exclusiveOrExpression ('|' rhs=exclusiveOrExpression)*
	;

exclusiveOrExpression
	:	lhs=andExpression ('^' rhs=andExpression)*
	;

andExpression
	:	lhs=equalityExpression ('&' rhs=equalityExpression)*
	;

equalityExpression
	:	lhs=relationalExpression (op=('==' | '!=') rhs=relationalExpression)*		# EqualStandardExpression
	|	lhs=relationalExpression (op=('~' | '!~') rhs=regularExpression)*			# MatchRegexExpression
	|	lhs=relationalExpression (op=('~' | '!~') rhs=aclReferenceExpression)*		# MatchAclExpression
	;

relationalExpression
	:	lhs=additiveExpression (op=('<' | '>' | '<=' | '>=') rhs=additiveExpression)*
	;

additiveExpression
	:	lhs=multiplicativeExpression (op=('+' | '-') rhs=multiplicativeExpression)*
	;

multiplicativeExpression
	:	lhs=unaryExpression (op=('*' | '/' | '%') rhs=unaryExpression)*
	;

unaryExpression
	:	primaryExpression			# UnaryPassthrough
	|	op='!' unaryExpression		# UnaryNegateExpression
	;

primaryExpression
	:	globalFunctionExpression
	|	memberAccessExpression
	|	literalExpression
	|	'(' expression ')'
	;

globalFunctionExpression
	:	'boltsort.sort' '(' url=stringLiteral ')'															# GlobalUrlSort
	|	'cstr_escape' '(' cstr=stringLiteral ')'															# GlobalStringEscape
	|	'http_status_when' '(' statusCode=integerLiteral ',' commaSeparatedStatusCodes=stringLiteral ')'	# GlobalHttpStatusWhen
	|	'std.atoi' '(' text=stringLiteral ')'																# GlobalAtoI
	|	'std.strstr' '(' haystack=stringLiteral ',' needle=stringLiteral ')'								# GlobalStrStr
	|	'std.strtol' '(' text=stringLiteral ',' base=integerLiteral ')'										# GlobalStrToL
	|	'std.tolower' '(' text=stringLiteral ')'															# GlobalToLower
	|	'std.toupper' '(' text=stringLiteral ')'															# GlobalToUpper
	|	('std.ip' | 'std.str2ip') '(' address=stringLiteral ',' fallback=stringLiteral ')'					# GlobalStrToIp
	|	'std.strlen' '(' text=stringLiteral ')'																# GlobalStrLen
	|	'subfield' '(' header=stringLiteral ',' fieldName=stringLiteral (',' sep=stringLiteral)? ')'		# GlobalSubField
	|	'urlencode' '(' text=stringLiteral ')'																# GlobalUrlEncode
	|	'urldecode' '(' text=stringLiteral ')'																# GlobalUrlDecode
	;

memberAccessExpression
	:	lhs=Identifier ('.' rhs=Identifier)*
	;

literalExpression
	:	booleanLiteral
	|	timeLiteral
	|	stringLiteral
	|	synthenticLiteral
	|	integerLiteral
	;

regularExpression
	:	StringConstant
	;

aclReferenceExpression
	:	name=Identifier
	;

stringLiteral
	:	StringConstant
	;

synthenticLiteral
	:	SyntheticString
	;

integerLiteral
	:	IntegerConstant
	;

timeLiteral
	:	TimeConstant
	;

booleanLiteral
	:	BooleanConstant
	;

/*
 * Lexer Rules
 */

VclIdentifier
	:	'vcl_' IdentifierNondigit IdentifierAny*
	;

Identifier
    :   IdentifierNondigit IdentifierAny*
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

AclExclude
	:	'!'
	;

fragment
IdentifierNondigit
    :   Nondigit
    ;

fragment
IdentifierAny
	:	Nondigit
    |   Digit
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
	:	~["\r\n]
	|	HexEncoding
	;

fragment
Nondigit
    :   [a-zA-Z_\-]
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
