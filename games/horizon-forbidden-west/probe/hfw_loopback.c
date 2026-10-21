/* hfw_loopback.c — offline pose exchange on 127.0.0.1.
 *
 * Two UDP sockets on the loopback interface trade one hfw_pose (48 bytes:
 * 3 doubles position, 3 doubles forward). Neither binds a public address.
 * No game process. Exit 0 only if the received bytes match the sent bytes.
 *
 * Build: cl /O2 /GS- hfw_loopback.c ws2_32.lib /Fe:hfw_loopback.exe
 */
#define WIN32_LEAN_AND_MEAN
#include <winsock2.h>
#include <ws2tcpip.h>
#include <stdio.h>
#include <string.h>
#include "hfw_pose.h"

int main(void)
{
 WSADATA wsa;
 SOCKET rx = INVALID_SOCKET;
 SOCKET tx = INVALID_SOCKET;
 struct sockaddr_in addr;
 struct sockaddr_in from;
 int from_len;
 hfw_pose sent;
 hfw_pose got;
 int n;
 int port;

 if (WSAStartup(MAKEWORD(2, 2), &wsa) != 0) {
 fprintf(stderr, "FAIL WSAStartup\n");
 return 1;
 }

 rx = socket(AF_INET, SOCK_DGRAM, IPPROTO_UDP);
 tx = socket(AF_INET, SOCK_DGRAM, IPPROTO_UDP);
 if (rx == INVALID_SOCKET || tx == INVALID_SOCKET) {
 fprintf(stderr, "FAIL socket\n");
 goto bad;
 }

 memset(&addr, 0, sizeof(addr));
 addr.sin_family = AF_INET;
 addr.sin_port = 0;
 addr.sin_addr.s_addr = htonl(INADDR_LOOPBACK);
 if (bind(rx, (struct sockaddr *)&addr, sizeof(addr)) != 0) {
 fprintf(stderr, "FAIL bind 127.0.0.1\n");
 goto bad;
 }
 from_len = sizeof(addr);
 if (getsockname(rx, (struct sockaddr *)&addr, &from_len) != 0) {
 fprintf(stderr, "FAIL getsockname\n");
 goto bad;
 }
 port = ntohs(addr.sin_port);
 if (addr.sin_addr.s_addr != htonl(INADDR_LOOPBACK) || port == 0) {
 fprintf(stderr, "FAIL not loopback\n");
 goto bad;
 }

 /* Walk-run sample 0, decoded from the old= qwords. Facing is a stand-in. */
 sent.pos_x = 5991.074;
 sent.pos_y = -2886.996;
 sent.pos_z = 334.957;
 sent.fwd_x = 0.0;
 sent.fwd_y = 0.0;
 sent.fwd_z = 1.0;

 n = sendto(tx, (const char *)&sent, HFW_POSE_BYTES, 0,
 (struct sockaddr *)&addr, sizeof(addr));
 if (n != HFW_POSE_BYTES) {
 fprintf(stderr, "FAIL sendto %d\n", n);
 goto bad;
 }

 memset(&got, 0, sizeof(got));
 from_len = sizeof(from);
 n = recvfrom(rx, (char *)&got, HFW_POSE_BYTES, 0,
 (struct sockaddr *)&from, &from_len);
 if (n != HFW_POSE_BYTES) {
 fprintf(stderr, "FAIL recvfrom %d\n", n);
 goto bad;
 }
 if (from.sin_addr.s_addr != htonl(INADDR_LOOPBACK)) {
 fprintf(stderr, "FAIL peer not loopback\n");
 goto bad;
 }
 if (memcmp(&sent, &got, HFW_POSE_BYTES) != 0) {
 fprintf(stderr, "FAIL bytes differ\n");
 goto bad;
 }

 printf("LOOPBACK OK 127.0.0.1:%d %d bytes pos=(%.3f,%.3f,%.3f) fwd=(%.3f,%.3f,%.3f)\n",
 port, n, got.pos_x, got.pos_y, got.pos_z, got.fwd_x, got.fwd_y, got.fwd_z);
 closesocket(rx);
 closesocket(tx);
 WSACleanup();
 return 0;

bad:
 if (rx != INVALID_SOCKET) closesocket(rx);
 if (tx != INVALID_SOCKET) closesocket(tx);
 WSACleanup();
 return 1;
}
