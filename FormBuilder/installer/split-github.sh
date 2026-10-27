#!/usr/bin/env bash
# 把 FormBuilder/ 與 ERPClient/ 兩個資料夾的提交歷史抽成一條獨立的分支 github-split（保留資料夾路徑），
# 用來推到 GitHub 的 ralfyao/FormBuilder。不改工作目錄，只新增 Git 物件與 github-split 分支。
# FormBuilder/docs/README.github.md 會放到 GitHub 儲存庫根目錄的 README.md。
# 同樣的來源歷史每次產生的提交雜湊都一樣，所以之後重跑再推送會是正常的往前推（不必強制推送）。
# 用法：在 YJF_Program_2024 根目錄執行  bash FormBuilder/installer/split-github.sh [來源分支，預設 HEAD]
#       再推送：git push formbuilder github-split:main
set -euo pipefail
src="${1:-HEAD}"
parent=""
parent_tree=""
count=0
for c in $(git rev-list --reverse --topo-order "$src" -- FormBuilder ERPClient); do
    entries=""
    for d in FormBuilder ERPClient; do
        if t=$(git rev-parse -q --verify "$c:$d" 2>/dev/null); then
            entries+=$(printf '040000 tree %s\t%s' "$t" "$d")$'\n'
        fi
    done
    # GitHub 首頁的 README.md：放在 FormBuilder/docs/README.github.md，同步時放到根目錄
    if b=$(git rev-parse -q --verify "$c:FormBuilder/docs/README.github.md" 2>/dev/null); then
        entries+=$(printf '100644 blob %s\t%s' "$b" "README.md")$'\n'
    fi
    tree=$(printf '%s' "$entries" | git mktree)
    [ "$tree" = "$parent_tree" ] && continue          # 這兩個資料夾沒變的提交跳過
    IFS=$'\x01' read -r an ae ad cn ce cd < <(git log -1 --format='%an%x01%ae%x01%ad%x01%cn%x01%ce%x01%cd' --date=raw "$c")
    new=$(git log -1 --format=%B "$c" | GIT_AUTHOR_NAME="$an" GIT_AUTHOR_EMAIL="$ae" GIT_AUTHOR_DATE="$ad" \
          GIT_COMMITTER_NAME="$cn" GIT_COMMITTER_EMAIL="$ce" GIT_COMMITTER_DATE="$cd" \
          git commit-tree "$tree" ${parent:+-p "$parent"})
    parent="$new"
    parent_tree="$tree"
    count=$((count + 1))
done
git branch -f github-split "$parent"
echo "github-split -> $parent（$count 筆提交）"
