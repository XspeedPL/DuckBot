grammar DuckScript;

content: (CONTENT | function)+;

function: FUNC_START FUNC_NAME (ARG_PART content (ARG_SEPARATOR content)*)? FUNC_END;

FUNC_START: '{';
FUNC_NAME: [a-zA-Z0-9]+;
FUNC_END: '}';

ARG_PART: ':';
ARG_SEPARATOR: ',';

ESC_CHAR: '\\';
ESCAPE: ESC_CHAR (ARG_PART | ARG_SEPARATOR | FUNC_START | FUNC_END);
CONTENT: (ESCAPE | ~[{}:,])+;
