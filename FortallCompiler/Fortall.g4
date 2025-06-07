grammar Fortall;

/*
Parser
*/

program: toplevel+ EOF;
toplevel: field | function;
field: TYPE ID ';' | TYPE ID ':=' constant ';';
constant: NUMBER | STRING | BOOL;
function: 'Funcao' ID 'Recebe' (paramList| 'Nada') 'e Retorna' (TYPE|'Nada') block 'Fim Funcao';
paramList: param (',' param)*;
param: TYPE ID;
block: statement*;
statement: declaration | assignment | ifStatement | whileStatement | returnStatement | ioStatement | (functionCall';') | ';';
functionCall: ID '(' (expression (',' expression)*)? ')';
declaration: TYPE ID ';' | TYPE ID ':=' expression ';';
assignment: ID ':=' expression ';';
ifStatement: 'Se' expression 'Entao' block ('Senao' block)? 'Fim Se';
whileStatement: 'Enquanto' expression 'Faca' block 'Fim Enquanto';
returnStatement: 'Retorna' expression ';' | 'Retorna Nada;';
ioStatement: 'Escrever' expression ';' | 'Ler' ID ';';

// expression with precedence
expression: orExpr;
orExpr: andExpr ('||' andExpr)*;
andExpr: equalityExpr ('&&' equalityExpr)*;
equalityExpr: relationalExpr (('==' | '!=') relationalExpr)*;
relationalExpr: addExpr (('<' | '<=' | '>' | '>=') addExpr)*;
addExpr: mulExpr (('+' | '-') mulExpr)*;
mulExpr: unaryExpr (('*' | '/') unaryExpr)*;
unaryExpr: '!' unaryExpr | primary;
primary: ID | constant | functionCall | '(' expression ')';

/*
Lexer
*/
TYPE: 'int' | 'bool' | 'str';
BOOL: 'true' | 'false';
ID: [a-zA-Z_][a-zA-Z0-9_]*;
NUMBER: ('-')?[0-9]+;
STRING: '"' (~["\r\n])* '"';
WS: [ \t\r\n]+ -> skip; // Skip whitespace
