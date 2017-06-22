grammar DuckScript;

content: (IDENTIFIER | ESCAPE | EXTRAS | function)+;

function: FUNC_START IDENTIFIER (ARG_PART content? (ARG_SEPARATOR content?)*)? FUNC_END;

ESCAPE: '^' [{}:,];
FUNC_START: '{';
IDENTIFIER: [a-zA-Z0-9]+;
FUNC_END: '}';
ARG_PART: ':';
ARG_SEPARATOR: ',';
EXTRAS: ~[a-zA-Z0-9{}:,]+;
