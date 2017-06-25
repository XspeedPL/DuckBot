grammar DuckSwitch;

content: value optionCase+ optionDefault?;

optionCase: CASESEP SPACES CASE SPACES QUOTE value? QUOTE SPACES? value?;
optionDefault: CASESEP SPACES DEFAULT SPACES? value?;

value: (LITERALS | QUOTE+ | ESCAPE+ | SPACES)+;

CASE: 'case';
DEFAULT: 'default';

QUOTE: '"';
ESCAPE: '^|';
CASESEP: '|';
SPACES: [ \t]+;

LITERALS: ~["| \t]+;
