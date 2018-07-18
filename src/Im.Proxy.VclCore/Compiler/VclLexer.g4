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

Semi
	:	';'
	;

LBrace
	:	'{'
		-> pushMode(BLOCK)
	;

mode BLOCK;

RBrace
	:	'}'
		-> popMode
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
