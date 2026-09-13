#version 330 core
       
layout (location = 0) in vec3 vPos;
layout (location = 1) in vec4 vCol;
    
uniform mat4 uModel;    

out vec4 fCol;

void main()
{
    gl_Position =  uModel * vec4(vPos, 1.0);
    fCol = vCol;
}