lexer grammar VclLexer;

/*
 * Lexer Rules
 */

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

ExpectedResponse
	:	'expected_response'
	;

HashData
	:	'hash_data'
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

IfToken
	:	'if'
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

Url
	:	'url'
	;

Timeout
	:	'timeout'
	;

Interval
	:	'interval'
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

BackendRequest
	:	'bereq'
	;

BackendResponse
	:	'beresp'
	;

Request
	:	'req'
	;

Response
	:	'resp'
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

Unset
	:	'unset'
	;

Set
	:	'set'
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

Pipe
	:	'pipe'
	;

HitForPass
	:	'hit-for-pass'
	;

Pass
	:	'pass'
	;

Hit
	:	'hit'
	;

Miss
	:	'miss'
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

SyntheticString
	:	'{"' .*? '"}'
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

Semi
	:	';'
	;

LBrace
	:	'{'
	;

RBrace
	:	'}'
	;

BooleanConstant
	:	BoolFalse
	|	BoolTrue
	;

TimeConstant
	:	DigitChar+ ('ms' | 's' | 'm' | 'h' | 'd' | 'w' | 'y')
	;

IntegerConstant
	:	DigitChar+
	;

IdentifierWithHyphen
	:   IdentifierNondigit IdentifierAnyWithHyphen*
    ;

HexEncoding
	:	(PercentChar [a-fA-F0-9] [a-fA-F0-9]) +
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
	:	SlashChar
	;

Percent
	:	PercentChar
	;

Hat
	:	'^'
	;

Comma
	:	','
	;

Dot
	:	DotChar
	;

LParens
	:	'('
	;

RParens
	:	')'
	;

Minus
	:	HyphenChar
	;

SubnetMask
	:	IpAddressSequence SlashChar DigitChar+
	;

IpAddress
	:	IpAddressSequence
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

BlockComment
    :   '/*' .*? '*/'
        -> skip
    ;

LineComment
    :   ('# ' | '//') ~[\r\n]*
        -> skip
    ;

fragment
SystemSubPrefix
	:	'vcl_'
	;

fragment
IdentifierNondigit
    :   NondigitChar
    ;

fragment
IdentifierAny
	:	NondigitChar
    |   DigitChar
	;

fragment
IdentifierAnyWithHyphen
	:	NondigitChar
	|	DigitChar
	|	HyphenChar
	;

fragment
IpAddressSequence
	:	'"' DigitChar+ DotChar DigitChar+ DotChar DigitChar+ DotChar DigitChar+ '"'
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
NondigitChar
    :   [a-zA-Z_]
    ;

fragment
DigitChar
    :   [0-9]
    ;

fragment
HyphenChar
	:	'-'
	;

fragment
SlashChar
	:	'/'
	;

fragment
DotChar
	:	'.'
	;

fragment
PercentChar
	:	'%'
	;

fragment
BoolFalse
	:	'false'
	;

fragment
BoolTrue
	:	'true'
	;
