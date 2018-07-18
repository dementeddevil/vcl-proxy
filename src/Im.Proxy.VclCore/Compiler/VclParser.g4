parser grammar VclParser;

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
	:	Declare Local VarToken Dot name=Identifier type=(TypeBool | TypeInt | TypeFloat | TypeTime | TypeRtime | TypeString) Semi
	;

ifStatement
	:	IfToken LParens test=conditionalOrExpression RParens ifTrue=statement ((Elseif | Elsif | Elif) LParens otherTest=conditionalOrExpression RParens otherTrue=statement)* (Else elseStmt=statement)?
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
	:	Equal | PlusEqual | MinusEqual
	;

conditionalExpression
	:	IfToken LParens conditionalOrExpression Comma ifTrue=expression Comma ifFalse=expression RParens
	;

conditionalOrExpression
	:	lhs=conditionalAndExpression (DoubleOr rhs=conditionalAndExpression)*
	;

conditionalAndExpression
	:	lhs=inclusiveOrExpression (DoubleAnd rhs=inclusiveOrExpression)*
	;

inclusiveOrExpression
	:	lhs=exclusiveOrExpression (Or rhs=exclusiveOrExpression)*
	;

exclusiveOrExpression
	:	lhs=andExpression (Hat rhs=andExpression)*
	;

andExpression
	:	lhs=equalityExpression (And rhs=equalityExpression)*
	;

equalityExpression
	:	lhs=relationalExpression (op=(DoubleEqual | ExclaimEqual) rhs=relationalExpression)*	# EqualStandardExpression
	|	lhs=relationalExpression (op=(Tilde | ExclaimTilde) rhs=regularExpression)*				# MatchRegexExpression
	|	lhs=relationalExpression (op=(Tilde | ExclaimTilde) rhs=aclReferenceExpression)*		# MatchAclExpression
	;

relationalExpression
	:	lhs=additiveExpression (op=(LessThan | GreaterThan | LessThanEqual | GreaterThanEqual) rhs=additiveExpression)*
	;

additiveExpression
	:	lhs=multiplicativeExpression (op=(Plus | Minus) rhs=multiplicativeExpression)*
	;

multiplicativeExpression
	:	lhs=unaryExpression (op=(Star | Slash | Percent) rhs=unaryExpression)*
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
	:	Now																			# GlobalNow
	|	lhs=Identifier (Dot rhs=Identifier)? LParens argumentList RParens			# GlobalMethod
	;

argumentList
	:	expression
	|	argumentList Comma expression
	;

memberAccessExpression
	:	obj=contextTransferObjects Dot Http Dot header=IdentifierWithHyphen			# AccessMemberHttp
	|	obj=contextAllObjects Dot name=(Identifier | Backend)						# AccessMemberNormal
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
	|	VarToken
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
