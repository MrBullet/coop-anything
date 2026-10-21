/* hfw_pose.h — one pose packet, 127.0.0.1 only.
 *
 * Layout, little-endian, 48 bytes, no padding:
 * double pos_x, pos_y, pos_z world position in metres (entity+0xD8)
 * double fwd_x, fwd_y, fwd_z facing, also metres-free unit doubles
 *
 * The Mat34 forward on this build did not track the walk, so the packet
 * does not carry those floats. A later writer can put a real facing in
 * the second triple; the loopback only checks that the bytes survive.
 *
 * Both sockets bind 127.0.0.1. There is no INADDR_ANY and no public port.
 */
#ifndef HFW_POSE_H
#define HFW_POSE_H

#include <stdint.h>

#define HFW_POSE_BYTES 48

#pragma pack(push, 1)
typedef struct hfw_pose {
 double pos_x, pos_y, pos_z;
 double fwd_x, fwd_y, fwd_z;
} hfw_pose;
#pragma pack(pop)

#endif
