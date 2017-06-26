grammar DuckScript;

script: content? EOF;

content: (IDENTIFIER | VALUE | function)+;

function: FUNC_START IDENTIFIER (ARG_PART content? (ARG_SEPARATOR content?)*)? FUNC_END;

FUNC_START: '{';
IDENTIFIER: [a-zA-Z0-9]+;
FUNC_END: '}';
ARG_PART: ':';
ARG_SEPARATOR: ',';
VALUE: (~[{}:,^|] | '^' [{}:,^|])+;
