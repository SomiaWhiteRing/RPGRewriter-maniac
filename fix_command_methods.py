import re
import os
import sys

def fix_command_file(file_path):
    print(f"处理文件: {file_path}")
    
    # 读取原文件内容
    with open(file_path, 'r', encoding='utf-8') as f:
        content = f.read()
    
    # 创建备份
    backup_path = file_path + '.backup'
    with open(backup_path, 'w', encoding='utf-8') as f:
        f.write(content)
    print(f"已创建备份文件: {backup_path}")
    
    # 查找所有形如 command12345XXX() 的方法
    # 正则表达式匹配整个方法，从方法声明到结束大括号
    # 使用非贪婪匹配确保正确找到方法结尾
    method_pattern = re.compile(r'(\s+string\s+command\d+[A-Za-z]+\(\).*?\{)(.*?)(\n\s+\})', re.DOTALL)
    methods = method_pattern.finditer(content)
    
    # 用于存储已处理的方法
    processed_methods = []
    
    # 处理每个方法
    for method in methods:
        method_decl = method.group(1)  # 方法声明部分，包括开始的大括号
        method_body = method.group(2)  # 方法主体
        method_end = method.group(3)   # 结束的大括号
        method_start = method.start()
        method_full_end = method.end()
        
        # 如果方法中包含 args[，但不包含 HasEnoughArgs 和 SafeGetArg，则需要修复
        if 'args[' in method_body and 'HasEnoughArgs' not in method_body and 'SafeGetArg' not in method_body:
            # 获取方法名
            method_name_match = re.search(r'string\s+(command\d+[A-Za-z]+)\(\)', method_decl)
            if method_name_match:
                method_name = method_name_match.group(1)
                print(f"正在修复方法: {method_name}")
                
                # 找出方法中使用到的args索引
                args_indices = re.findall(r'args\[(\d+)\]', method_body)
                if not args_indices:
                    continue
                    
                # 计算需要的参数数量
                max_index = max([int(idx) for idx in args_indices]) + 1
                
                # 创建安全检查代码
                check_code = f"""
            if (!HasEnoughArgs({max_index}))
            {{
                return "{method_name}: [Error: Invalid arguments]";
            }}
            """
                
                # 替换args[x]为SafeGetArg(x)
                fixed_body = method_body
                fixed_body = re.sub(r'args\[(\d+)\]', r'SafeGetArg(\1)', fixed_body)
                
                # 处理特殊的args.Length条件检查
                fixed_body = re.sub(r'args\.Length\s*>\s*(\d+)', r'HasEnoughArgs(\1 + 1)', fixed_body)
                
                # 构建修复后的完整方法
                fixed_method = method_decl + check_code + fixed_body + method_end
                processed_methods.append((method_start, method_full_end, fixed_method))
    
    # 从后向前替换，避免位置偏移问题
    processed_methods.sort(reverse=True, key=lambda x: x[0])
    new_content = content
    for start, end, fixed_method in processed_methods:
        new_content = new_content[:start] + fixed_method + new_content[end:]
    
    # 写入修改后的内容
    with open(file_path, 'w', encoding='utf-8') as f:
        f.write(new_content)
    
    print(f"已修复 {len(processed_methods)} 个方法")
    return len(processed_methods)

def main():
    script_dir = os.path.dirname(os.path.abspath(__file__))
    command_file = os.path.join(script_dir, 'Command.cs')
    
    if not os.path.exists(command_file):
        print(f"错误: 找不到文件 {command_file}")
        return
    
    fixed_count = fix_command_file(command_file)
    print(f"修复完成! 总共修复了 {fixed_count} 个方法。")
    print("请重新编译项目并测试。")

if __name__ == "__main__":
    main() 