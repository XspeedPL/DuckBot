grammar DuckSwitch;

content: VALUE optionCase+ optionDefault? EOF;

optionCase: CASE QUOTE VALUE? QUOTE VALUE?;
optionDefault: DEFAULT VALUE?;

CASE: '|' [ \t]* 'case';
DEFAULT: '|' [ \t]* 'default';

QUOTE: '"';

WS: [ \t\r\n]+ -> skip;

VALUE: (~["| \t\r\n] | '^'["|])((~["|] | '^'["|])*(~["| \t\r\n] | '^'["|]))?;
