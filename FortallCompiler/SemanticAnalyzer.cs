using FortallCompiler.Ast;
using Type = FortallCompiler.Ast.Type;

namespace FortallCompiler;

public class SemanticAnalyzer {

    private ScopeData globalScope = new();
    
    private readonly List<Diagnostic> diagnostics = [];
    
    private readonly Dictionary<string,string> stringLiterals = new();
    private readonly Dictionary<string,string> invertedStringLiterals = new();
    private int stringLiteralCounter = 0;
    
    public (bool success, List<Diagnostic> diagnostics) Analyze(ProgramNode ast) {

        // first step: verificar se o programa tem uma main
        bool hasMain = ast.TopLevelNodes
            .Where(x => x is FunctionNode)
            .Cast<FunctionNode>()
            .Any(x => x.Name.Equals("main", StringComparison.CurrentCultureIgnoreCase));

        if (!hasMain) {
            diagnostics.Add(new Diagnostic("O programa deve conter uma função 'main' definida.", 0,0));
        }
        
        ast.ScopeData = globalScope;
        
        // indexar nome das funcoes
        ast.TopLevelNodes
            .Where(x => x is FunctionNode)
            .Cast<FunctionNode>()
            .ForEach(x => {
                if (globalScope.AvailableFunctionNames.Contains(x.Name)) {
                    diagnostics.Add(new Diagnostic($"A função '{x.Name}' já foi declarada.", x.LineNumber, x.ColumnNumber));
                    return;
                }
                globalScope.AvailableFunctionNames.Add(x.Name);
                globalScope.Functions.Add(x.Name,x);
            });
        
        // indexar nome das variaveis globais
        ast.TopLevelNodes
            .Where(x => x is FieldDeclarationNode)
            .Cast<FieldDeclarationNode>()
            .ForEach(x => {
                if (globalScope.AvailableVariableNames.Contains(x.FieldName)) {
                    diagnostics.Add(new Diagnostic($"A variável global '{x.FieldName}' já foi declarada.", x.LineNumber, x.ColumnNumber));
                    return;
                }
                globalScope.AvailableVariableNames.Add(x.FieldName);
                globalScope.Variables.Add(x.FieldName, new VariableData(x));
            });
        
        // verificar conflitos de nomes globais vs funcoes
        foreach (KeyValuePair<string, VariableData> glob in globalScope.Variables) {
            FieldDeclarationNode variable = glob.Value.FieldDeclaration!;
            if (globalScope.AvailableFunctionNames.Contains(variable.FieldName)) {
                diagnostics.Add(new Diagnostic($"O nome '{glob}' está sendo usado tanto como variável global quanto como função.", variable.LineNumber, variable.ColumnNumber));
            }
        }
        
        // verificar se cada uma das variaveis globais com valor estao com valor correto
        foreach (string glob in globalScope.AvailableVariableNames) {
            FieldDeclarationNode fieldNode = globalScope.Variables[glob].FieldDeclaration!;
            if (fieldNode.InitValue is null) {
                continue;
            }
            // verificar se o tipo do valor inicial é compatível com o tipo da variável
            if (fieldNode.FieldType != fieldNode.InitValue.Type) {
                diagnostics.Add(new Diagnostic($"O valor inicial da variável global '{glob}' não é do tipo '{fieldNode.FieldType}'.", fieldNode.LineNumber, fieldNode.ColumnNumber));
            }
        }
        
        // agora parte para as verificacoes de cada funcao
        ast.TopLevelNodes
            .Where(x => x is FunctionNode)
            .Cast<FunctionNode>()
            .ForEach(CheckFunction);

        
        ast.StringLiterals = stringLiterals;
        return (diagnostics.Count == 0, diagnostics);
    }
    
    private void CheckFunction(FunctionNode function) {
        ScopeData scope = new();
        scope.ParentScope = globalScope;
        
        // add parameters to scope
        foreach (ParameterNode param in function.Parameters) {
            if (scope.VariableExists(param.Name)) {
                diagnostics.Add(new Diagnostic($"O parâmetro '{param.Name}' já foi declarado.", param.LineNumber, param.ColumnNumber));
                continue;
            }
            scope.AvailableVariableNames.Add(param.Name);
            scope.Variables.Add(param.Name, new VariableData(param));
        }
        
        CheckBlock(function.Body, scope, function.ReturnType);
    }

    private void CheckExpression(ExpressionNode expression, ScopeData scope)
    {
        if (expression is BinaryExpressionNode binExpr) {
            if (binExpr.Left.ExpressionType == Type.Void)
            {
                TryGetExpressionType(binExpr.Left, scope);
            }
            if (binExpr.Right.ExpressionType == Type.Void)
            {
                TryGetExpressionType(binExpr.Right, scope);
            }
            
            switch (binExpr.Operation)
            {
                case BinaryOperationType.And:
                case BinaryOperationType.Or:
                    if (binExpr.Left.ExpressionType != Type.Boolean || binExpr.Right.ExpressionType != Type.Boolean) {
                        diagnostics.Add(new Diagnostic("Operadores lógicos 'and' e 'or' só podem ser usados com expressões booleanas.", binExpr.LineNumber, binExpr.ColumnNumber));
                    }
                    break;
                case BinaryOperationType.Addition:
                case BinaryOperationType.Division:
                case BinaryOperationType.Multiplication:
                case BinaryOperationType.Subtraction:
                    if (binExpr.Left.ExpressionType != Type.Integer || binExpr.Right.ExpressionType != Type.Integer) {
                        diagnostics.Add(new Diagnostic("Operadores aritméticos só podem ser usados com expressões inteiras.", binExpr.LineNumber, binExpr.ColumnNumber));
                    }
                    break;
                case BinaryOperationType.Equals:
                case BinaryOperationType.GreaterEqualThan:
                case BinaryOperationType.GreaterThan:
                case BinaryOperationType.LessEqualThan:
                case BinaryOperationType.LessThan:
                case BinaryOperationType.NotEquals:
                    if (binExpr.Left.ExpressionType != binExpr.Right.ExpressionType) {
                        diagnostics.Add(new Diagnostic("Operadores de comparação só podem ser usados com expressões do mesmo tipo.", binExpr.LineNumber, binExpr.ColumnNumber));
                        break;
                    }

                    if (binExpr.Left.ExpressionType == Type.String)
                    {
                        diagnostics.Add(new Diagnostic("Operadores de comparação não são permitidos com expressões do tipo string.", binExpr.LineNumber, binExpr.ColumnNumber));
                    }
                    else if (binExpr.Left.ExpressionType == Type.Void || binExpr.Right.ExpressionType == Type.Void) {
                        diagnostics.Add(new Diagnostic("Operadores de comparação não podem ser usados com expressões do tipo void.", binExpr.LineNumber, binExpr.ColumnNumber));
                    }
                    break;


                default:
                    throw new NotSupportedException("Operacao binaria nao suportada: " + binExpr.Operation);
            }
            
            CheckExpression(binExpr.Left, scope);
            CheckExpression(binExpr.Right, scope);
        }
        else if (expression is UnaryExpressionNode unaryExpr) {
            if (unaryExpr.Operand.ExpressionType == Type.Void) {
                TryGetExpressionType(unaryExpr.Operand, scope);
            }

            switch (unaryExpr.Operation) {
                case UnaryOperationType.Not:
                    if (unaryExpr.Operand.ExpressionType != Type.Boolean) {
                        diagnostics.Add(new Diagnostic("Operador 'not' só pode ser usado com expressões booleanas.", unaryExpr.LineNumber, unaryExpr.ColumnNumber));
                    }
                    break;
                default:
                    throw new NotSupportedException("Operacao unaria nao suportada: " + unaryExpr.Operation);
            }
            
            CheckExpression(unaryExpr.Operand, scope);
        }
        else if(expression is FunctionCallExpressionNode funcCallExpr)
        {
            if(!scope.FunctionExists(funcCallExpr.FunctionName))
            {
                diagnostics.Add(new Diagnostic("Função chamada não foi declarada: " + funcCallExpr.FunctionName, funcCallExpr.LineNumber, funcCallExpr.ColumnNumber));
                return;
            }
            FunctionNode func = scope.GetFunction(funcCallExpr.FunctionName)!;

            if (func.ReturnType == Type.Void)
            {
                diagnostics.Add(new Diagnostic($"Função '{funcCallExpr.FunctionName}' não pode ser chamada em uma expressão, pois retorna void.", funcCallExpr.LineNumber, funcCallExpr.ColumnNumber));
                return;
            }
            
            if(funcCallExpr.Arguments.Count != func.Parameters.Count)
            {
                diagnostics.Add(new Diagnostic($"Número de argumentos passados para a função '{funcCallExpr.FunctionName}' não corresponde ao número de parâmetros esperados.", funcCallExpr.LineNumber, funcCallExpr.ColumnNumber));
                return;
            }

            for (int i = 0; i < funcCallExpr.Arguments.Count; i++)
            {
                var expectedType = func.Parameters[i].ParameterType;
                var argExpr = funcCallExpr.Arguments[i];
                if (argExpr.ExpressionType == Type.Void) {
                    bool success = TryGetExpressionType(argExpr, scope);
                    if (!success) {
                        diagnostics.Add(new Diagnostic($"Não foi possível deduzir o tipo do argumento {i + 1} da função '{funcCallExpr.FunctionName}'.", argExpr.LineNumber, argExpr.ColumnNumber));
                        continue;
                    }
                }
                if (argExpr.ExpressionType != expectedType)
                {
                    diagnostics.Add(new Diagnostic($"Tipo do argumento {i + 1} da função '{funcCallExpr.FunctionName}' não corresponde ao tipo esperado '{expectedType}'.", argExpr.LineNumber, argExpr.ColumnNumber));
                }
            }
        }
        else if (expression is IdentifierExpressionNode idExpr)
        {
            if (!scope.VariableExists(idExpr.Name))
            {
                diagnostics.Add(new Diagnostic($"Variável '{idExpr.Name}' não foi declarada.", idExpr.LineNumber, idExpr.ColumnNumber));
                return;
            }
            VariableData varData = scope.GetVariable(idExpr.Name)!;
            Type t = varData.Type;
            idExpr.ExpressionType = t;
        }
        else if (expression is LiteralExpressionNode litExpr)
        {
            // se for string, indexa nas literais de string
            if (litExpr.Type != Type.String)
            {
                return;
            }

            string literal = litExpr.Value as string ?? string.Empty;
            if (invertedStringLiterals.TryGetValue(literal, out string? key))
            {
                litExpr.StringIdentifier = key;
            }
            else
            {
                string identifier = litExpr.StringIdentifier ?? $"string_literal_{stringLiteralCounter++}";
                litExpr.StringIdentifier = identifier;
                stringLiterals.Add(identifier, literal);
                invertedStringLiterals.Add(literal, identifier);
            }
        }
        else
        {
            Console.WriteLine("wut. cheguei numa expressao que nao sei o que fazer: " + expression.GetType().Name);
        }
    }

    private void CheckBlock(BlockNode block, ScopeData scope, Type currentFunctionReturnType)
    {
        block.ScopeData = scope;
        foreach (StatementNode stmt in block.Statements) {
            // verifica se a variavel declarada ja existe
            if (stmt is VariableDeclarationNode varDeclNode) {
                if (scope.VariableExists(varDeclNode.VariableName)) {
                    diagnostics.Add(new Diagnostic($"Variavel {varDeclNode.VariableName} ja esta declarada em outro lugar", varDeclNode.LineNumber, varDeclNode.ColumnNumber));
                }
                else {
                    // adicionar
                    scope.AvailableVariableNames.Add(varDeclNode.VariableName);
                    scope.Variables.Add(varDeclNode.VariableName, new VariableData(varDeclNode));
                }

                // se tem valor iniciado, verifica se condiz
                if (varDeclNode.InitValue is not null) {
                    if (varDeclNode.InitValue.ExpressionType == Type.Void) {
                        // tenta descobrir 
                        bool searchSuccess = TryGetExpressionType(varDeclNode.InitValue, scope);
                        if (!searchSuccess) {
                            Console.WriteLine("Erro brabo. Nao consegui deduzir o tipo de alguma expressao que estava como void :O");
                            continue;
                        }
                    }
                    
                    CheckExpression(varDeclNode.InitValue, scope);
                    
                    if (varDeclNode.InitValue.ExpressionType != varDeclNode.VariableType) {
                        diagnostics.Add(new Diagnostic("Tipo gerado pela expressao nao bate com o tipo da variavel", varDeclNode.InitValue.LineNumber, varDeclNode.InitValue.ColumnNumber));
                    }
                }
                continue;
            }
            else if (stmt is AssignmentNode assignmentNode) {
                if (!scope.VariableExists(assignmentNode.VariableName)) {
                    diagnostics.Add(new Diagnostic("A variavel nao foi declarada!", assignmentNode.LineNumber, assignmentNode.ColumnNumber));
                    continue;
                }
                // verifica se o tipo bate com o valor dado
                VariableData varData = scope.GetVariable(assignmentNode.VariableName)!;
                if (assignmentNode.AssignedValue.ExpressionType == Type.Void)
                {
                    if (!TryGetExpressionType(assignmentNode.AssignedValue, scope))
                    {
                        diagnostics.Add(new Diagnostic("Nao foi possivel deduzir o tipo da expressao de atribuicao.", assignmentNode.AssignedValue.LineNumber, assignmentNode.AssignedValue.ColumnNumber));
                        continue;
                    }
                }
                
                CheckExpression(assignmentNode.AssignedValue, scope);

                if (assignmentNode.AssignedValue.ExpressionType != varData.Type)
                {
                    diagnostics.Add(new Diagnostic($"O tipo da expressão atribuída não corresponde ao tipo da variável. Esperava {varData.Type} e recebi {assignmentNode.AssignedValue.ExpressionType}", assignmentNode.LineNumber, assignmentNode.ColumnNumber));
                }
            }
            else if (stmt is IfStatementNode ifStmtNode)
            {
                // verificar expressao condicional
                if(ifStmtNode.Condition.ExpressionType == Type.Void)
                {
                    if (!TryGetExpressionType(ifStmtNode.Condition, scope))
                    {
                        diagnostics.Add(new Diagnostic("Não foi possível deduzir o tipo da expressão condicional do if.", ifStmtNode.Condition.LineNumber, ifStmtNode.Condition.ColumnNumber));
                    }
                }
                CheckExpression(ifStmtNode.Condition, scope);

                if (ifStmtNode.Condition.ExpressionType != Type.Boolean)
                {
                    diagnostics.Add(new Diagnostic("A expressão condicional do if deve ser do tipo booleano.", ifStmtNode.Condition.LineNumber, ifStmtNode.Condition.ColumnNumber));
                }
                
                // verificar bloco then
                ScopeData thenScope = new() { ParentScope = scope };
                CheckBlock(ifStmtNode.ThenBlock, thenScope, currentFunctionReturnType);

                if (ifStmtNode.ElseBlock is not null)
                {
                    // verificar bloco else
                    ScopeData elseScope = new() { ParentScope = scope };
                    CheckBlock(ifStmtNode.ElseBlock, elseScope, currentFunctionReturnType);
                }
            }else if (stmt is WhileStatementNode whileStmtNode)
            {
                // verificar expressao condicional
                if(whileStmtNode.Condition.ExpressionType == Type.Void)
                {
                    if (!TryGetExpressionType(whileStmtNode.Condition, scope))
                    {
                        diagnostics.Add(new Diagnostic("Não foi possível deduzir o tipo da expressão condicional do while.", whileStmtNode.Condition.LineNumber, whileStmtNode.Condition.ColumnNumber));
                    }
                }
                CheckExpression(whileStmtNode.Condition, scope);
                if (whileStmtNode.Condition.ExpressionType != Type.Boolean)
                {
                    diagnostics.Add(new Diagnostic("A expressão condicional do if deve ser do tipo booleano.", whileStmtNode.Condition.LineNumber, whileStmtNode.Condition.ColumnNumber));
                }
                
                // verificar bloco do while
                ScopeData whileScope = new() { ParentScope = scope };
                CheckBlock(whileStmtNode.Body, whileScope, currentFunctionReturnType);
            }
            else if (stmt is ReturnStatementNode returnStmtNode)
            {
                if (currentFunctionReturnType != Type.Void)
                {
                    continue;
                }
                
                if (returnStmtNode.Expression is null)
                {
                    diagnostics.Add(new Diagnostic("Função com retorno deve conter apenas Returns com expressões.", returnStmtNode.LineNumber, returnStmtNode.ColumnNumber));
                    continue;
                }

                CheckExpression(returnStmtNode.Expression, scope);

                if (returnStmtNode.Expression.ExpressionType == Type.Void)
                {
                    if (!TryGetExpressionType(returnStmtNode.Expression, scope))
                    {
                        diagnostics.Add(new Diagnostic("Não foi possível deduzir o tipo da expressão de retorno.", returnStmtNode.Expression.LineNumber, returnStmtNode.Expression.ColumnNumber));
                        continue;
                    }
                } 
                
                if (returnStmtNode.Expression.ExpressionType != currentFunctionReturnType)
                {
                    diagnostics.Add(new Diagnostic($"Tipo de retorno da função não corresponde ao tipo esperado. Esperava {currentFunctionReturnType} e recebi {returnStmtNode.Expression.ExpressionType}.", returnStmtNode.LineNumber, returnStmtNode.ColumnNumber));
                }
            }else if (stmt is WriteNode writeNode)
            {
                if (writeNode.Expression.ExpressionType == Type.Void)
                {
                    if (!TryGetExpressionType(writeNode.Expression, scope))
                    {
                        diagnostics.Add(new Diagnostic("Não foi possível deduzir o tipo da expressão de escrita.", writeNode.Expression.LineNumber, writeNode.Expression.ColumnNumber));
                        continue;
                    }
                }
                
                CheckExpression(writeNode.Expression, scope);
                if (writeNode.Expression.ExpressionType == Type.Void)
                {
                    diagnostics.Add(new Diagnostic("A expressão de escrita não pode ser do tipo void.", writeNode.Expression.LineNumber, writeNode.Expression.ColumnNumber));
                }
            }else if (stmt is ReadNode readNode)
            {
                if (!scope.VariableExists(readNode.VariableName))
                {
                    diagnostics.Add(new Diagnostic($"Variável '{readNode.VariableName}' não foi declarada.", readNode.LineNumber, readNode.ColumnNumber));
                    continue;
                }
            }else if (stmt is FunctionCallStatementNode funcCallStmt)
            {
                // verificar se ela existe
                FunctionCallExpressionNode funcCallExpr = funcCallStmt.FunctionCallExpression;
                if(!scope.FunctionExists(funcCallExpr.FunctionName))
                {
                    diagnostics.Add(new Diagnostic("Função chamada não foi declarada: " + funcCallExpr.FunctionName, funcCallExpr.LineNumber, funcCallExpr.ColumnNumber));
                    continue;
                }
                FunctionNode func = scope.GetFunction(funcCallExpr.FunctionName)!;
                if(funcCallExpr.Arguments.Count != func.Parameters.Count)
                {
                    diagnostics.Add(new Diagnostic($"Número de argumentos passados para a função '{funcCallExpr.FunctionName}' não corresponde ao número de parâmetros esperados.", funcCallExpr.LineNumber, funcCallExpr.ColumnNumber));
                    continue;
                }

                // verifica os argumentos e parametros
                for (int i = 0; i < funcCallExpr.Arguments.Count; i++)
                {
                    var expectedType = func.Parameters[i].ParameterType;
                    var argExpr = funcCallExpr.Arguments[i];
                    if (argExpr.ExpressionType == Type.Void)
                    {
                        bool success = TryGetExpressionType(argExpr, scope);
                        if (!success)
                        {
                            diagnostics.Add(new Diagnostic(
                                $"Não foi possível deduzir o tipo do argumento {i + 1} da função '{funcCallExpr.FunctionName}'.",
                                argExpr.LineNumber, argExpr.ColumnNumber));
                            continue;
                        }
                    }

                    if (argExpr.ExpressionType != expectedType)
                    {
                        diagnostics.Add(new Diagnostic(
                            $"Tipo do argumento {i + 1} da função '{funcCallExpr.FunctionName}' não corresponde ao tipo esperado '{expectedType}'.",
                            argExpr.LineNumber, argExpr.ColumnNumber));
                    }
                }
            }
        }
    }
    
    private bool TryGetExpressionType(ExpressionNode expr, ScopeData scope) {
        if (expr.ExpressionType != Type.Void) {
            return true;
        }

        if (expr is FunctionCallExpressionNode funcExpr) {
            string funcName = funcExpr.FunctionName;
            if (!scope.FunctionExists(funcName)) {
                diagnostics.Add(new Diagnostic($"Função '{funcName}' não foi declarada.", funcExpr.LineNumber, funcExpr.ColumnNumber));
                return false;
            }
            FunctionNode func = scope.GetFunction(funcName)!;
            funcExpr.ExpressionType = func.ReturnType;
            return true;
        }else if (expr is IdentifierExpressionNode idExpr) {
            string varName = idExpr.Name;
            if (!scope.VariableExists(varName)) {
                diagnostics.Add(new Diagnostic($"Variável '{varName}' não foi declarada.", idExpr.LineNumber, idExpr.ColumnNumber));
                return false;
            }
            VariableData varData = scope.GetVariable(varName)!;
            Type t = varData.Type;
            idExpr.ExpressionType = t;
            return true;
        }

        Console.WriteLine("erro brabo, nao consegui deduzir o tipo de uma expressao que era void :O");
        
        return false;
    }
    
    
    public class ScopeData {
        public ScopeData? ParentScope { get; set; }

        public List<string> AvailableFunctionNames { get; set; } = [];
        public List<string> AvailableVariableNames { get; set; } = [];

        public Dictionary<string, FunctionNode> Functions { get; set; } = [];
        public Dictionary<string, VariableData> Variables { get; set; } = [];

        public bool VariableExists(string name) {
            if (AvailableVariableNames.Contains(name))
            {
                return true;
            }
            return ParentScope is not null && ParentScope.VariableExists(name);
        }

        public VariableData? GetVariable(string name) {
            if (Variables.TryGetValue(name, out VariableData? data)) {
                return data;
            }

            VariableData? parentData = ParentScope?.GetVariable(name);
            return parentData;
        }

        public bool FunctionExists(string name) {
            if (AvailableFunctionNames.Contains(name)) {
                return true;
            }
            return ParentScope is not null && ParentScope.FunctionExists(name);
        }

        public FunctionNode? GetFunction(string name) {
            if (Functions.TryGetValue(name, out FunctionNode? data)) {
                return data;
            }

            FunctionNode? parentData = ParentScope?.GetFunction(name);
            return parentData;
        }
    }

    // classe intermediaria pra agrupar fields e variables
    public class VariableData {
        public FieldDeclarationNode? FieldDeclaration {get; set; }
        public VariableDeclarationNode? VariableDeclarationNode { get; set; }
        public ParameterNode? ParameterNode { get; set; }
        
        public Type Type => FieldDeclaration?.FieldType ?? VariableDeclarationNode?.VariableType ?? ParameterNode!.ParameterType;

        public VariableData(FieldDeclarationNode field)
        {
            FieldDeclaration = field;
        }

        public VariableData(VariableDeclarationNode variable)
        {
            VariableDeclarationNode = variable;
        }
        
        public VariableData(ParameterNode parameter)
        {
            ParameterNode = parameter;
        }
    }
}