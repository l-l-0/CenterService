
``` shell

# 创建分支
git branch user1

# 提交分支到服务器
git push origin user1

# 切换分支
git checkout user1

```

``` shell

# 和主分支合并代码
git add .
git commit -m 修改了代码什么内容
git checkout master
git pull
git checkout user1
git merge master

# 提交到 user1 分支
git push --set-upstream origin user1

# 删除分支
git checkout master
git branch -d user1
git push origin :user1

```