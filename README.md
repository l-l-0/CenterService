
``` shell

# 创建分支
git branch BJTT

# 提交分支到服务器
git push origin BJTT

# 切换分支
git checkout BJTT

```

``` shell

# 和主分支合并代码
git add .
git commit -m 修改了代码什么内容
git checkout master
git pull
git checkout BJTT
git merge master

# 提交到 BJTT 分支
git push --set-upstream origin BJTT

# 删除分支
git checkout master
git branch -d BJTT
git push origin :BJTT

```