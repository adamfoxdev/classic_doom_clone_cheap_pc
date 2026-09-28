#!/usr/bin/env python3
"""
Stone-block puzzle solver, used to design the Hanging Cisterns' three ledges (src/Level.cs).

A breadth-first search over pushes and pulls, as the game plays them: E pushes a block one cell away from you,
Shift+E pulls it one cell toward you as you step back, and you can walk anywhere on the ledge that isn't a pillar or
a block. It prints the shortest solution for each ledge (in moves) with and without pulls, and the moves in the form
Headless.CisternSolutions uses, in map coordinates: (pull, x, y, dir) = stand at (x, y); the block moves `dir`.

Grids: '.' floor, '#' pillar, 'X' block, '^' plate, '*' block on a plate, '@' the checkpoint pad where you land
(blocks can't cross a pad, and the solver checks none needs to).

    python3 tools/puzzles/solve_blocks.py
"""
from collections import deque
import sys

def parse(rows):
    walls=set(); blocks=[]; plates=set(); start=None
    for y,r in enumerate(rows):
        for x,c in enumerate(r):
            if c=='#': walls.add((x,y))
            if c in 'X*': blocks.append((x,y))
            if c in '^*': plates.add((x,y))
            if c=='@': start=(x,y)
    return walls, frozenset(blocks), plates, start, len(rows[0]), len(rows)

D=[(1,0,'E'),(-1,0,'W'),(0,1,'S'),(0,-1,'N')]

def solve(rows, allow_pull=True, max_states=2_000_000):
    walls, blocks, plates, start, W, H = parse(rows)
    def free(p, bl): 
        x,y=p
        return 0<=x<W and 0<=y<H and p not in walls and p not in bl
    def reach(p, bl):
        seen={p}; q=deque([p])
        while q:
            c=q.popleft()
            for dx,dy,_ in D:
                n=(c[0]+dx,c[1]+dy)
                if n not in seen and free(n,bl): seen.add(n); q.append(n)
        return seen
    def norm(p, bl):
        return min(reach(p,bl))
    s0=(norm(start,blocks), blocks)
    prev={s0:None}; q=deque([s0])
    while q:
        st=q.popleft()
        p,bl=st
        if all(b in plates for b in bl) and len(bl)==len(plates) or plates<=bl:
            # reconstruct
            path=[]
            while prev[st] is not None:
                st,mv=prev[st]
                path.append(mv)
            return path[::-1]
        area=reach(p,bl)
        for b in bl:
            for dx,dy,name in D:
                # push: stand at b-d, block to b+d
                stand=(b[0]-dx,b[1]-dy); to=(b[0]+dx,b[1]+dy)
                if stand in area and free(to,bl):
                    nb=frozenset(bl-{b}|{to})
                    ns=(norm(b,nb),nb)
                    if ns not in prev: prev[ns]=(st,('push',stand,name,b)); q.append(ns)
                if allow_pull:
                    # pull: stand at b+d (facing block, direction -d), player steps to b+2d, block to b+d
                    stand=(b[0]+dx,b[1]+dy); back=(b[0]+2*dx,b[1]+2*dy)
                    if stand in area and free(back,bl):
                        nb=frozenset(bl-{b}|{stand})
                        ns=(norm(back,nb),nb)
                        if ns not in prev: prev[ns]=(st,('pull',stand,name,b)); q.append(ns)
        if len(prev)>max_states: return None
    return None



# the ledges, as placed in the map: (name, top-left x, top-left y, grid)
LEDGES = [
    ("low ledge", 10, 14, ["@......",
                           "..X.#..",
                           "...#.^.",
                           ".#.X...",
                           "...^..."]),
    ("middle ledge", 19, 13, ["@........",
                              ".#..X..#.",
                              "...#.....",
                              "..X...#^.",
                              "....#....",
                              ".^......."]),
    ("high ledge", 21, 5, ["@.#.....",
                           "..X.X.#.",
                           ".#...#..",
                           "..#.X..^",
                           "^...#..^"]),
]

if __name__ == '__main__':
    step = {'E': (1, 0), 'W': (-1, 0), 'S': (0, 1), 'N': (0, -1)}
    for name, x0, y0, grid in LEDGES:
        push_only = solve(grid, allow_pull=False)
        best = solve(grid, allow_pull=True)
        print(f"{name}: {len(best)} moves" + (f" ({len(push_only)} pushing only)" if push_only else " (it needs a pull)"))
        _, _, _, pad, _, _ = parse(grid)
        for kind, stand, d, b in best:
            dx, dy = step[d]
            assert (b[0] + dx, b[1] + dy) != pad, "a block would have to cross the checkpoint pad"
        print("    " + ", ".join(f"({'true' if k == 'pull' else 'false'}, {s[0] + x0}, {s[1] + y0}, '{d}')" for k, s, d, _ in best))
