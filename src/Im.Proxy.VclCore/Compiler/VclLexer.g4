lexer grammar VclLexer;

/*
 * Lexer Rules
 */

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

LBrace
	:	'{'
		-> pushMode(BLOCK)
	;

Semi
	:	';'
	;

mode BLOCK;

RBrace
	:	'}'
		-> popMode
	;

IfToken
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

Now
	:	'now'
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

VarToken
	:	'var'
	;

TypeBool
	:	'BOOL'
	;

TypeInt
	:	'INTEGER'
	;

TypeFloat
	:	'FLOAT'
	;

TypeTime
	:	'TIME'
	;

TypeRtime
	:	'RTIME'
	;

TypeString
	:	'STRING'
	;

BoolFalse
	:	'false'
	;

BoolTrue
	:	'true'
	;

BooleanConstant
	:	BoolFalse
	|	BoolTrue
	;

TimeConstant
	:	Digit+ ('ms' | 's' | 'm' | 'h' | 'd' | 'w' | 'y')
	;

IntegerConstant
	:	Digit+
	;

StringConstant
	:	'"' CharacterSequence? '"'
	;

VclIdentifier
	:	SystemSubPrefix IdentifierNondigit IdentifierAny*
	;

Identifier
    :   IdentifierNondigit IdentifierAny*
    ;

IdentifierWithHyphen
	:   IdentifierNondigit IdentifierAnyWithHyphen*
    ;

DoubleEqual
	:	'=='
	;

ExclaimEqual
	:	'!='
	;

PlusEqual
	:	'+='
	;

MinusEqual
	:	'-='
	;

LessThanEqual
	:	'<='
	;

GreaterThanEqual
	:	'>='
	;

DoubleOr
	:	'||'
	;

DoubleAnd
	:	'&&'
	;

ExclaimTilde
	:	'!~'
	;

Equal
	:	'='
	;

Exclaim
	:	'!'
	;

Tilde
	:	'~'
	;

LessThan
	:	'<'
	;

GreaterThan
	:	'>'
	;

Or
	:	'|'
	;

And
	:	'&'
	;

Plus
	:	'+'
	;

Star
	:	'*'
	;

Slash
	:	'/'
	;

Percent
	:	'%'
	;

Hat
	:	'^'
	;

Comma
	:	','
	;

Dot
	:	'.'
	;

LParens
	:	'('
	;

RParens
	:	')'
	;

fragment
SystemSubPrefix
	:	'vcl_'
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
