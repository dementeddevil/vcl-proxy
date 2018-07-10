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
	:	Include StringConstant Semi
	;

backendDeclaration
	:	Backend Identifier LBrace backendElementList RBrace
	;

backendElementList
	:	backendElement
	|	backendElementList backendElement
	;

backendElement
	:	Dot backendVariableExpression
	;

backendVariableExpression
	:	backendStringVariableExpression Semi
	|	backendIntegerVariableExpression Semi
	|	backendTimeVariableExpression Semi
	|	backendProbeVariableExpression
	;

backendStringVariableExpression
	:	name=backendStringVariableName Equal value=stringLiteral
	;

backendIntegerVariableExpression
	:	name=backendIntegerVariableName Equal value=integerLiteral
	;

backendTimeVariableExpression
	:	name=backendTimeVariableName Equal value=timeLiteral
	;

backendProbeVariableExpression
	:	name=Probe Equal value=probeExpression
	;

backendStringVariableName
	:	Host
	|	Port
	|	HostHeader
	|	ProxyHeader
	;

backendIntegerVariableName
	:	MaxConnections
	;

backendTimeVariableName
	:	ConnectionTimeout
	|	FirstByteTimeout
	|	BetweenBytesTimeout
	;

probeExpression
	:	probeReferenceExpression
	|	probeInlineExpression
	;

probeReferenceExpression
	:	probeName=Identifier Semi
	;

probeDeclaration
	:	Probe Identifier probeInlineExpression
	;

probeInlineExpression
	:	LBrace probeElementList RBrace
	;

probeElementList
	:	probeElement
	|	probeElementList probeElement
	;

probeElement
	:	Dot probeVariableExpression Semi
	;

probeVariableExpression
	:	probeStringVariableExpression
	|	probeIntegerVariableExpression
	|	probeTimeVariableExpression
	;

probeStringVariableExpression
	:	name=probeStringVariableName Equal value=stringLiteral
	;

probeIntegerVariableExpression
	:	name=probeIntegerVariableName Equal value=integerLiteral
	;

probeTimeVariableExpression
	:	name=probeTimeVariableName Equal value=timeLiteral
	;

probeStringVariableName
	:	Url
	;

probeTimeVariableName
	:	Timeout
	|	Interval
	;

probeIntegerVariableName
	:	ExpectedResponse
	|	Initial
	|	Window
	|	Threshold
	;

aclDeclaration
	:	Acl name=Identifier LBrace aclElementList RBrace
	;

aclElementList
	:	aclElement
	|	aclElementList aclElement
	;

aclElement
	:	exclude=Exclaim? aclIpAddressOrHost
	;

aclIpAddressOrHost
	:	host=StringConstant						# AclEntryNonIgnorableHost
	|	LParens host=StringConstant RParens		# AclEntryIgnorableHost
	|	address=IpAddress						# AclEntryIpAddress
	|	subnet=SubnetMask						# AclEntrySubnetMask
	;

procedureDeclaration
	:	Sub name=VclIdentifier compoundStatement		{InCustomFunction = false;}		# SystemProcedureDeclaration
	|	Sub name=Identifier compoundStatement			{InCustomFunction = true;}		# CustomProcedureDeclaration
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
	:	expression? Semi
	;

varStatement
	:	Declare Local Var Dot name=Identifier type=('BOOL' | 'INTEGER' | 'FLOAT' | 'TIME' | 'RTIME' | 'STRING') Semi
	;

ifStatement
	:	If LParens test=conditionalOrExpression RParens ifTrue=statement ((Elseif | Elsif | Elif) LParens otherTest=conditionalOrExpression RParens otherTrue=statement)* (Else elseStmt=statement)?
	;

setStatement
	:	Set lhs=memberAccessExpression Equal rhs=expression Semi
	;

removeStatement
	:	(Remove | Unset) id=memberAccessExpression Semi
	;

errorStatement
	:	Error statusCode=IntegerConstant statusDescription=StringConstant Semi
	;

syntheticStatement
	:	Synthetic stringLiteral Semi
	;

callStatement
	:	Call subroutineName=Identifier Semi
	;

hashDataStatement
	:	HashData LParens expr=expression RParens Semi
	;

returnStatement
	:	{InCustomFunction}? Return Semi
	|	Return LParens returnStateExpression RParens Semi
	;

restartStatement
	:	Restart Semi
	;

returnStateExpression
	:	simpleReturnStateExpression
	|	complexReturnStateExpression
	;
	
simpleReturnStateExpression
	:	Restart
	|	Receive
	|	Hash
	|	Lookup
	|	Busy
	|	Purge
	|	Pass
	|	Pipe
	|	Hit
	|	Miss
	|	HitForPass
	|	Fetch
	|	Deliver
	|	Done
	|	Abandon
	|	Retry
	|	Error
	;

complexReturnStateExpression
	:	returnSynthStateExpression
	;

returnSynthStateExpression
	:	Synth LParens statusCode=IntegerConstant (Comma statusDescription=StringConstant )? RParens
	;

compoundStatement
	:	LBrace blockItemList? RBrace
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
	:	Equal | '+=' | '-='
	;

conditionalExpression
	:	If LParens conditionalOrExpression Comma ifTrue=expression Comma ifFalse=expression RParens
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
	:	primaryExpression				# UnaryPassthrough
	|	op=Exclaim unaryExpression		# UnaryNegateExpression
	;

primaryExpression
	:	globalFunctionExpression
	|	memberAccessExpression
	|	literalExpression
	|	LParens expression RParens
	;

globalFunctionExpression
	:	'now'																											# GlobalNow
	|	'boltsort' Dot 'sort' LParens url=stringLiteral RParens															# GlobalUrlSort
	|	'cstr_escape' LParens cstr=stringLiteral RParens																# GlobalStringEscape
	|	'http_status_when' LParens statusCode=integerLiteral Comma commaSeparatedStatusCodes=stringLiteral RParens		# GlobalHttpStatusWhen
	|	Std Dot 'atoi' LParens text=stringLiteral RParens																# GlobalAtoI
	|	Std Dot 'strstr' LParens haystack=stringLiteral Comma needle=stringLiteral RParens								# GlobalStrStr
	|	Std Dot 'strtol' LParens text=stringLiteral Comma base=integerLiteral RParens									# GlobalStrToL
	|	Std Dot 'tolower' LParens text=stringLiteral RParens															# GlobalToLower
	|	Std Dot 'toupper' LParens text=stringLiteral RParens															# GlobalToUpper
	|	Std Dot ('ip' | 'str2ip') LParens address=stringLiteral Comma fallback=stringLiteral RParens					# GlobalStrToIp
	|	Std Dot 'strlen' LParens text=stringLiteral RParens																# GlobalStrLen
	|	'subfield' LParens header=stringLiteral Comma fieldName=stringLiteral (Comma sep=stringLiteral)? RParens		# GlobalSubField
	|	'urlencode' LParens text=stringLiteral RParens																	# GlobalUrlEncode
	|	'urldecode' LParens text=stringLiteral RParens																	# GlobalUrlDecode
	;

memberAccessExpression
	:	obj=contextTransferObjects Dot Http Dot header=IdentifierWithHyphen												# AccessMemberHttp
	|	obj=contextAllObjects Dot name=Identifier																		# AccessMemberNormal
	;

contextTransferObjects
	:	Request
	|	Response
	|	BackendRequest
	|	BackendResponse
	;

contextAllObjects
	:	contextTransferObjects
	|	Client
	|	Server
	|	Local
	|	Remote
	|	Var
	;

literalExpression
	:	booleanLiteral
	|	timeLiteral
	|	stringLiteral
	|	synthenticLiteral
	|	integerLiteral
	;

regularExpression
	:	value=StringConstant
	;

aclReferenceExpression
	:	value=Identifier
	;

stringLiteral
	:	value=StringConstant
	;

synthenticLiteral
	:	value=SyntheticString
	;

integerLiteral
	:	value=IntegerConstant
	;

timeLiteral
	:	value=TimeConstant
	;

booleanLiteral
	:	value=BooleanConstant
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

IdentifierWithHyphen
	:   IdentifierNondigit IdentifierAnyWithHyphen*
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

Exclaim
	:	'!'
	;

Comma
	:	','
	;

Backend
	:	'backend'
	;

Include
	:	'include'
	;

Probe
	:	'probe'
	;

Acl
	:	'acl'
	;

Sub
	:	'sub'
	;

If
	:	'if'
	;

Elseif
	:	'elseif'
	;

Elsif
	:	'elsif'
	;

Elif
	:	'elif'
	;

Else
	:	'else'
	;

Synthetic
	:	'synthetic'
	;

Call
	:	'call'
	;

Return
	:	'return'
	;

HashData
	:	'hash_data'
	;

Synth
	:	'synth'
	;

Std
	:	'std'
	;

Http
	:	'http'
	;

Host
	:	'host'
	;

Port
	:	'port'
	;

HostHeader
	:	'host_header'
	;

ProxyHeader
	:	'proxy_header'
	;

MaxConnections
	:	'max_connections'
	;

ConnectionTimeout
	:	'connection_timeout'
	;

FirstByteTimeout
	:	'first_byte_timeout'
	;

BetweenBytesTimeout
	:	'between_bytes_timeout'
	;

Url
	:	'url'
	;

Timeout
	:	'timeout'
	;

Interval
	:	'interval'
	;

ExpectedResponse
	:	'expected_response'
	;

Initial
	:	'initial'
	;

Window
	:	'window'
	;

Threshold
	:	'threshold'
	;

Request
	:	'req'
	;

Response
	:	'resp'
	;

BackendRequest
	:	'bereq'
	;

BackendResponse
	:	'beresp'
	;

Client
	:	'client'
	;

Server
	:	'server'
	;

Local
	:	'local'
	;

Remote
	:	'remote'
	;

Declare
	:	'declare'
	;

Set
	:	'set'
	;

Unset
	:	'unset'
	;

Remove
	:	'remove'
	;

Error
	:	'error'
	;

Restart
	:	'restart'
	;

Receive
	:	'receive'
	;

Hash
	:	'hash'
	;

Lookup
	:	'lookup'
	;

Busy
	:	'busy'
	;

Purge
	:	'purge'
	;

Pass
	:	'pass'
	;

Pipe
	:	'pipe'
	;

Hit
	:	'hit'
	;

Miss
	:	'miss'
	;

HitForPass
	:	'hit-for-pass'
	;

Fetch
	:	'fetch'
	;

Deliver
	:	'deliver'
	;

Done
	:	'done'
	;

Abandon
	:	'abandon'
	;

Retry
	:	'retry'
	;

Var
	:	'var'
	;

Equal
	:	'='
	;

Dot
	:	'.'
	;

Semi
	:	';'
	;

LParens
	:	'('
	;

RParens
	:	')'
	;

LBrace
	:	'{'
	;

RBrace
	:	'}'
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

fragment
IdentifierAnyWithHyphen
	:	Nondigit
	|	Digit
	|	Hyphen
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
